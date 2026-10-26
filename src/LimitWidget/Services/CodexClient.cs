using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.IO;

namespace LimitWidget.Services;

/// <summary>Only account and quota RPCs. No conversations, agent turns or raw credential access.</summary>
public sealed class CodexClient : IDisposable
{
    readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pending = new();
    readonly SemaphoreSlim writer = new(1, 1);
    readonly CancellationTokenSource lifetime = new();
    Process? process;
    ChildProcessJob? job;
    Task responses = Task.CompletedTask, errors = Task.CompletedTask;
    readonly object lifecycle = new();
    readonly List<Task> writes = new();
    long sequence;
    LoginAttempt? loginAttempt;
    public bool IsRunning => !disposed && process is { HasExited: false };
    public bool SharedSession { get; private set; }
    public bool PersistentSession { get; private set; }
    bool disposed;

    public static string? FindExecutable()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        return SelectExecutable(root, Environment.GetEnvironmentVariable("PATH") ?? "");
    }

    internal static string? SelectExecutable(string root, string searchPath)
    {
        if (Directory.Exists(root))
        {
            // Desktop updates install the active build in a versioned directory.
            // A legacy executable may remain in bin itself and lacks newer account/auth APIs.
            string? bundled = Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc)
                .Select(folder => Path.Combine(folder, "codex.exe")).FirstOrDefault(File.Exists);
            if (bundled != null) return bundled;
            string direct = Path.Combine(root, "codex.exe");
            if (File.Exists(direct)) return direct;
        }
        foreach (string folder in searchPath.Split(Path.PathSeparator))
        {
            string candidate;
            try { candidate = Path.Combine(folder.Trim('"'), "codex.exe"); } catch (ArgumentException) { continue; }
            if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public async Task StartAsync(bool sharedSession, bool rememberMe, CancellationToken cancellationToken)
    {
        if (process != null) throw new InvalidOperationException("Codex client already started.");
        string executable = FindExecutable() ?? throw new FileNotFoundException("Codex CLI was not found. Install Codex and restart the widget.");
        // Trust verification can consult Windows certificate services; keep it off the UI thread.
        using var verified = await Task.Run(() => TrustedExecutable.Open(executable), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SharedSession = sharedSession;
        PersistentSession = rememberMe && !sharedSession;
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (!sharedSession)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGptLimitWidget", "codex-home");
            Directory.CreateDirectory(home);
            info.Environment["CODEX_HOME"] = home;
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add($"cli_auth_credentials_store=\"{(rememberMe ? "keyring" : "ephemeral")}\"");
        }
        info.ArgumentList.Add("-c"); info.ArgumentList.Add("analytics.enabled=false");
        info.ArgumentList.Add("app-server"); info.ArgumentList.Add("--listen"); info.ArgumentList.Add("stdio://");
        job = new ChildProcessJob();
        try
        {
            process = Process.Start(info) ?? throw new IOException("Could not start Codex.");
            job.Assign(process);
        }
        catch { Dispose(); throw; }
        responses = ReadResponsesAsync(process);
        errors = DrainErrorsAsync(process);
        await RequestAsync("initialize", new { clientInfo = new { name = "codex_limit_widget", title = "Codex Limit Widget", version = "0.4.0" } }, cancellationToken);
        await WriteAsync(new { method = "initialized", @params = new { } }, cancellationToken);
    }

    public async Task<bool> HasAccountAsync(CancellationToken cancellationToken)
    {
        var result = await RequestAsync("account/read", new { refreshToken = false }, cancellationToken);
        return result.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object &&
               account.TryGetProperty("type", out var type) && type.GetString() == "chatgpt";
    }

    public async Task<QuotaSnapshot> ReadLimitsAsync(CancellationToken cancellationToken) =>
        QuotaSnapshot.Parse(await RequestAsync("account/rateLimits/read", new { }, cancellationToken));

    public async Task<UsageSummary> ReadUsageAsync(CancellationToken cancellationToken) =>
        UsageSummary.Parse(await RequestAsync("account/usage/read", new { }, cancellationToken));

    public async Task SignInAsync(Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        if (SharedSession) throw new InvalidOperationException("Sign-in is only allowed in the widget's isolated session.");
        if (loginAttempt != null) throw new InvalidOperationException("A sign-in attempt is already running.");
        var attempt = new LoginAttempt();
        loginAttempt = attempt;
        string? loginId = null;
        try
        {
            var result = await RequestAsync("account/login/start", new { type = "chatgpt" }, cancellationToken);
            loginId = result.GetProperty("loginId").GetString();
            attempt.SetId(loginId!);
            var uri = BrowserLogin.ValidateUrl(result.GetProperty("authUrl").GetString());
            openBrowser(uri);
            bool success = await attempt.Completion.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!success) throw new IOException("Sign-in was cancelled or failed. Please try again.");
        }
        catch
        {
            if (loginId != null && IsRunning)
            {
                try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await RequestAsync("account/login/cancel", new { loginId }, timeout.Token); } catch { }
            }
            throw;
        }
        finally { loginAttempt = null; }
    }

    public Task<JsonElement> SignOutAsync(CancellationToken cancellationToken)
    {
        if (SharedSession) throw new InvalidOperationException("The widget does not sign out the main Codex session.");
        return RequestAsync("account/logout", new { }, cancellationToken);
    }

    async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        long id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        try
        {
            await WriteAsync(new { id, method, @params = parameters }, cancellationToken);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(40), cancellationToken);
        }
        finally { pending.TryRemove(id, out _); }
    }

    Task WriteAsync(object message, CancellationToken cancellationToken)
    {
        lock (lifecycle)
        {
            if (disposed) throw new IOException("Codex connection is closed.");
            writes.RemoveAll(task => task.IsCompleted);
            Task task = WriteCoreAsync(message, cancellationToken); writes.Add(task); return task;
        }
    }

    async Task WriteCoreAsync(object message, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        cancellationToken = linked.Token;
        await writer.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning) throw new IOException("Codex connection is closed.");
            await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally { writer.Release(); }
    }

    async Task ReadResponsesAsync(Process child)
    {
        try
        {
            var reader = new BoundedLineReader(child.StandardOutput, 2_000_000);
            while (await reader.ReadAsync(lifetime.Token) is { } line)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out long number) && pending.TryRemove(number, out var completion))
                {
                    if (root.TryGetProperty("error", out _)) completion.TrySetException(new IOException("Codex rejected the account request. Check sign-in and network access."));
                    else if (root.TryGetProperty("result", out var result)) completion.TrySetResult(result.Clone());
                    else completion.TrySetException(new IOException("Codex returned an invalid response."));
                }
                else if (root.TryGetProperty("method", out var method) && method.GetString() == "account/login/completed")
                {
                    var parameters = root.GetProperty("params");
                    string? loginId = parameters.TryGetProperty("loginId", out var login) && login.ValueKind == JsonValueKind.String ? login.GetString() : null;
                    loginAttempt?.Notify(loginId, parameters.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True);
                }
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        finally
        {
            foreach (var item in pending.Values) item.TrySetException(new IOException("Codex connection closed. Retry refresh."));
            loginAttempt?.ConnectionClosed();
        }
    }

    async Task DrainErrorsAsync(Process child)
    {
        // Drain without persisting or showing raw output, which may contain account information.
        var buffer = new char[4096];
        try { while (await child.StandardError.ReadAsync(buffer.AsMemory(), lifetime.Token) != 0) { } }
        catch (Exception e) when (e is IOException or OperationCanceledException or InvalidOperationException) { }
    }

    public void Dispose()
    {
        Task[] active;
        lock (lifecycle)
        {
            if (disposed) return;
            disposed = true;
            active = writes.Append(responses).Append(errors).ToArray();
        }
        lifetime.Cancel();
        foreach (var completion in pending.Values) completion.TrySetException(new IOException("Codex connection closed."));
        loginAttempt?.ConnectionClosed();
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        finally { job?.Dispose(); }
        // Async readers/writers must finish before their synchronization objects and streams are disposed.
        _ = ReleaseResourcesAsync(active);
    }

    async Task ReleaseResourcesAsync(Task[] active)
    {
        try { await Task.WhenAll(active).ConfigureAwait(false); }
        catch (Exception) { /* Operations already report failures to their callers. */ }
        finally { process?.Dispose(); writer.Dispose(); lifetime.Dispose(); }
    }
}

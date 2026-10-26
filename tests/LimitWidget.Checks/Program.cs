using System.Text.Json;
using LimitWidget.Services;
using LimitWidget;

if (args.Contains("--oauth-probe"))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    using var client = new CodexClient();
    await client.StartAsync(false, false, cancellation.Token);
    bool officialUrlReceived = false;
    try
    {
        await client.SignInAsync(uri => { BrowserLogin.ValidateUrl(uri.AbsoluteUri); officialUrlReceived = true; cancellation.Cancel(); }, cancellation.Token);
        throw new Exception("Cancelled login unexpectedly succeeded");
    }
    catch (OperationCanceledException) when (officialUrlReceived) { }
    using var check = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    if (await client.HasAccountAsync(check.Token)) throw new Exception("Cancelled probe unexpectedly authenticated");
    Console.WriteLine("Official browser OAuth initialization and cancellation passed. No browser opened and no credentials saved.");
    return;
}

if (args.Contains("--isolated"))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(50));
    using var client = new CodexClient();
    await client.StartAsync(false, false, cancellation.Token);
    if (await client.HasAccountAsync(cancellation.Token)) throw new Exception("An ephemeral widget session must not inherit the main Codex login.");
    Console.WriteLine("Isolated ephemeral session does not inherit Codex credentials.");
    return;
}

if (args.Contains("--live"))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(55));
    using var client = new CodexClient();
    await client.StartAsync(true, true, cancellation.Token);
    Console.WriteLine($"ChatGPT-backed Codex session: {await client.HasAccountAsync(cancellation.Token)}");
    var quota = await client.ReadLimitsAsync(cancellation.Token);
    try
    {
        var usage = await client.ReadUsageAsync(cancellation.Token);
        Console.WriteLine($"Codex lifetime tokens: {usage.DisplayValue}");
    }
    catch (Exception error) when (error is IOException or TimeoutException)
    { Console.WriteLine("Optional lifetime token statistics unavailable from this Codex version."); }
    Console.WriteLine($"Primary: {quota.Primary?.RemainingPercent}% remaining, {quota.Primary?.DurationMinutes} min window");
    Console.WriteLine($"Secondary: {quota.Secondary?.RemainingPercent}% remaining, {quota.Secondary?.DurationMinutes} min window");
    return;
}

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
var lines = new BoundedLineReader(new StringReader("abc\r\n\nlast"), 4);
Check(await lines.ReadAsync(default) == "abc", "Bounded reader handles CRLF");
Check(await lines.ReadAsync(default) == "", "Bounded reader preserves empty lines");
Check(await lines.ReadAsync(default) == "last", "Bounded reader accepts exact limit and EOF");
Check(await lines.ReadAsync(default) == null, "Bounded reader reports EOF");
var flood = new BoundedLineReader(new StringReader(new string('x', 100_000)), 32);
try { await flood.ReadAsync(default); throw new Exception("Oversized line accepted"); }
catch (IOException) { checks++; }
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await new BoundedLineReader(new StringReader("data"), 32).ReadAsync(cancelled.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
}
using (var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ping.exe"))
    { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "-t", "127.0.0.1" }, RedirectStandardOutput = true })!)
{
    try
    {
        using (var job = new ChildProcessJob()) { job.Assign(child); }
        Check(child.WaitForExit(5000), "Closing job terminates its child process");
    }
    finally { if (!child.HasExited) child.Kill(); }
}
Check(ColorMath.FromHsv(0, 1, 1) == ((byte)255, (byte)0, (byte)0), "Hue red");
Check(ColorMath.FromHsv(120, 1, 1) == ((byte)0, (byte)255, (byte)0), "Hue green");
Check(ColorMath.FromHsv(240, 1, 1) == ((byte)0, (byte)0, (byte)255), "Hue blue");
Check(ColorMath.FromHsv(180, 0, 1) == ((byte)255, (byte)255, (byte)255), "Zero saturation is white at full brightness");
Check(ColorMath.FromHsv(180, 1, 0) == ((byte)0, (byte)0, (byte)0), "Zero brightness is black");
foreach (var rgb in new[] { ((byte)57, (byte)223, (byte)181), ((byte)171, (byte)85, (byte)245), ((byte)128, (byte)128, (byte)128) })
{
    var hsv = ColorMath.ToHsv(rgb.Item1, rgb.Item2, rgb.Item3);
    Check(ColorMath.FromHsv(hsv.Hue, hsv.Saturation, hsv.Value) == rgb, "RGB / HSV round trip");
}
UsageSummary ParseUsage(string json) { using var document = JsonDocument.Parse(json); return UsageSummary.Parse(document.RootElement); }
Check(ParseUsage("""{"summary":{"lifetimeTokens":290237562}}""").LifetimeTokens == 290237562, "Lifetime tokens preserve exact value");
Check(ParseUsage("""{"summary":{"lifetimeTokens":5000000000}}""").LifetimeTokens == 5000000000, "Lifetime tokens support 64-bit totals");
Check(ParseUsage("""{"summary":{"lifetimeTokens":0}}""").LifetimeTokens == 0, "Reported zero is valid");
foreach (string json in new[] { "{}", """{"summary":null}""", """{"summary":{"lifetimeTokens":null}}""", """{"summary":{"lifetimeTokens":-1}}""", """{"summary":{"lifetimeTokens":"unknown"}}""", """{"summary":{"lifetimeTokens":1e40}}""" })
    Check(ParseUsage(json).LifetimeTokens == null, "Unavailable token statistics must not become zero");
foreach (string value in new[] { "https://auth.openai.com/oauth/authorize?state=sample", "https://chatgpt.com/auth/login", "https://auth0.openai.com/authorize" })
    Check(BrowserLogin.ValidateUrl(value).Scheme == "https", "Official HTTPS login URL allowed");
foreach (string value in new[] { "http://auth.openai.com/login", "https://auth.openai.com.evil.example/login", "https://user@auth.openai.com/login", "https://auth.openai.com:8443/login", "file:///auth", "https://accounts.google.com/login" })
{
    try { BrowserLogin.ValidateUrl(value); throw new Exception("Unexpected URL accepted"); } catch (InvalidDataException) { checks++; }
}
var login = new LoginAttempt(); login.SetId("current"); login.Notify("old", true);
Check(!login.Completion.IsCompleted, "Old login notifications must be ignored");
login.Notify("current", true); Check(await login.Completion, "Current login can complete");
login = new LoginAttempt(); login.Notify("early", true); login.SetId("early");
Check(await login.Completion, "Notification before RPC response is correlated");
login = new LoginAttempt(); login.SetId("current"); login.Notify(null, true);
Check(!login.Completion.IsCompleted, "Notification without attempt ID cannot authenticate");
login.Notify("current", false); Check(!await login.Completion, "Provider rejection does not authenticate");
QuotaSnapshot Parse(string json) { using var doc = JsonDocument.Parse(json); return QuotaSnapshot.Parse(doc.RootElement); }
var snapshot = Parse("""{"rateLimits":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1793000000},"secondary":null}}""");
Check(snapshot.Primary?.RemainingPercent == 75, "Usage must be converted to remaining percentage");
Check(snapshot.Primary?.Label == "5h window", "Window label must use actual duration");
Check(snapshot.Secondary == null, "Missing quota is unknown, never zero");
Check(snapshot.Primary?.ResetsAt?.ToUnixTimeSeconds() == 1793000000, "Reset timestamps are seconds");
snapshot = Parse("""{"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":10},"secondary":{"usedPercent":110,"windowDurationMins":10080}}}}""");
Check(snapshot.Primary?.RemainingPercent == 90, "Multi-bucket Codex quota has priority");
Check(snapshot.Secondary?.RemainingPercent == 0, "Percentage is clamped");
Check(snapshot.Secondary?.Label == "Weekly", "Weekly window label");
snapshot = Parse("""{"rateLimits":{"primary":{"usedPercent":null},"secondary":{"usedPercent":20,"resetsAt":null,"windowDurationMins":null}}}""");
Check(snapshot.Primary == null, "Null percentage remains unknown");
Check(snapshot.Secondary?.ResetsAt == null, "Null reset time is allowed");
Check(snapshot.Secondary?.DurationMinutes == null, "Null duration is allowed");
foreach (string json in new[] { "{}", """{"rateLimitsByLimitId":{"other":{"primary":{"usedPercent":1}}}}""", """{"rateLimits":{"limitId":"other","primary":{"usedPercent":1}}}""", """{"rateLimits":{"primary":null}}""", """{"rateLimitsByLimitId":{"codex":null}}""" })
{
    try { Parse(json); throw new Exception("An unavailable quota must fail"); } catch (InvalidDataException) { checks++; }
}
string testFile = Path.Combine(AppContext.BaseDirectory, "settings-tests", "settings.json");
var preferences = new WidgetSettings { Width = 300, Topmost = true, RememberMe = false, SessionMode = "widget", RefreshMinutes = 30, Theme = "Light", Accent = "#123456" };
preferences.Save(testFile);
var loaded = WidgetSettings.Load(testFile);
Check(loaded.Width == 300 && loaded.Topmost && loaded.Theme == "Light", "Appearance survives save and reload");
Check(loaded.Accent == "#123456", "Codex accent survives save and reload");
Check(!loaded.RememberMe && loaded.SessionMode == "widget" && loaded.RefreshMinutes == 30, "Account preferences and interval survive save and reload");
Check(!File.Exists(testFile + ".tmp"), "Atomic save leaves no unfinished file");
File.WriteAllText(testFile, """{"ShowGpt":true,"ChatGptAccent":"#ABCDEF","Accent":"#654321","Width":340,"SessionMode":"widget"}""");
loaded = WidgetSettings.Load(testFile);
Check(loaded.Accent == "#654321" && loaded.Width == 340 && loaded.SessionMode == "widget", "Legacy settings preserve active preferences");
loaded.Save(testFile);
using (var cleaned = JsonDocument.Parse(File.ReadAllText(testFile)))
    Check(!cleaned.RootElement.TryGetProperty("ShowGpt", out _) && !cleaned.RootElement.TryGetProperty("ChatGptAccent", out _), "Removed feature settings are dropped on save");
File.WriteAllText(testFile, "invalid JSON");
Check(WidgetSettings.Load(testFile).RefreshMinutes == 10, "Corrupted settings recover to defaults");
Console.WriteLine($"Passed {checks} authentication, quota and settings checks.");

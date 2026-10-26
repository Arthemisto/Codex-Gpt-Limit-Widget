using System.IO;

namespace LimitWidget.Services;

internal static class BrowserLogin
{
    // Provider selection (including Google) belongs to the official ChatGPT page.
    public static Uri ValidateUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            uri.IdnHost is not ("auth.openai.com" or "chatgpt.com" or "auth0.openai.com"))
            throw new InvalidDataException("Codex returned an unexpected login URL.");
        return uri;
    }
}

internal sealed class LoginAttempt
{
    readonly object gate = new();
    readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly List<(string Id, bool Success)> earlyNotifications = new();
    string? expectedId;
    public Task<bool> Completion => completion.Task;

    public void SetId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Codex returned an invalid login attempt.");
        lock (gate)
        {
            expectedId = id;
            foreach (var notification in earlyNotifications)
                if (notification.Id == id) completion.TrySetResult(notification.Success);
            earlyNotifications.Clear();
        }
    }

    public void Notify(string? id, bool success)
    {
        if (string.IsNullOrEmpty(id)) return;
        lock (gate)
        {
            if (expectedId == null) { if (earlyNotifications.Count < 16) earlyNotifications.Add((id, success)); }
            else if (expectedId == id) completion.TrySetResult(success);
        }
    }

    public void ConnectionClosed() => completion.TrySetException(new IOException("Codex connection closed during sign-in."));
}

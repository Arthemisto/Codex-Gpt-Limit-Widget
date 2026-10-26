using System.ComponentModel;
using System.Text.Json;

namespace LimitWidget.Services;

internal static class ServiceErrors
{
    public static bool IsRecoverable(Exception error) => error is IOException or InvalidDataException
        or KeyNotFoundException or TimeoutException or UnauthorizedAccessException or OperationCanceledException
        or Win32Exception or InvalidOperationException or JsonException;

    public static bool IsStartupConflict(Exception error) => error is UnauthorizedAccessException
        or IOException or WaitHandleCannotBeOpenedException;
}

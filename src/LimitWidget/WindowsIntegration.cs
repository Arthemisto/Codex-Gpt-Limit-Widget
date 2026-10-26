using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace LimitWidget;

internal static class WindowsIntegration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string EntryName = "CodexGptLimitWidget";

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(EntryName) is string;
    }

    public static void SetStartup(bool enabled)
    {
        string? executable = Environment.ProcessPath;
        if (enabled && string.IsNullOrWhiteSpace(executable)) throw new IOException("The widget executable path is unavailable.");
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(EntryName, $"\"{executable}\"");
        else key.DeleteValue(EntryName, false);
    }

    public static void LaunchCodex() => Process.Start(new ProcessStartInfo("codex://") { UseShellExecute = true });

    public static void HideFromTaskSwitcher(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        const int extendedStyle = -20;
        const long toolWindow = 0x80, appWindow = 0x40000;
        long style = GetWindowLongPtr(handle, extendedStyle).ToInt64();
        SetWindowLongPtr(handle, extendedStyle, new IntPtr((style | toolWindow) & ~appWindow));
    }

    public static bool SystemLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
    }

    public static void EnsureVisible(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rectangle)) return;
        var monitor = MonitorFromRect(ref rectangle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        int width = rectangle.Right - rectangle.Left, height = rectangle.Bottom - rectangle.Top;
        int left = Math.Clamp(rectangle.Left, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - width));
        int top = Math.Clamp(rectangle.Top, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - height));
        if (left != rectangle.Left || top != rectangle.Top) SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, 0x0015);
    }

    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref Rect rectangle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}

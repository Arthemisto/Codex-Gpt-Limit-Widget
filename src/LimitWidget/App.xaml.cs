using System.Windows;
namespace LimitWidget;
public partial class App : Application
{
    Mutex? instance;
    EventWaitHandle? showEvent;
    RegisteredWaitHandle? showWait;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            var smokeWindow = new MainWindow(smokeTest: true); MainWindow = smokeWindow;
            smokeWindow.Loaded += async (_, _) => await UiSmokeChecks.RunAsync(smokeWindow, e.Args[1]);
            smokeWindow.Show(); return;
        }
        try
        {
        instance = new Mutex(true, @"Local\CodexGptLimitWidget", out bool first);
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CodexGptLimitWidget.Show");
        if (!first) { showEvent.Set(); Shutdown(); return; }
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.IO.IOException)
        {
            MessageBox.Show("The widget could not access its single-instance controls. Close any existing widget, including instances running as administrator, and try again.", "Codex Limit Widget", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(); return;
        }
        var window = new MainWindow(); MainWindow = window;
        if (e.Args.Contains("--sign-in"))
            window.Loaded += async (_, _) =>
            {
                while (window.Busy) await Task.Delay(100);
                window.ShowSettings();
                await window.SignInAsync();
            };
        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatcher.BeginInvoke(window.ShowWidget), null, Timeout.Infinite, false);
        window.Show();
        if (e.Args.Contains("--settings")) window.ShowSettings();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        showWait?.Unregister(null); showEvent?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}

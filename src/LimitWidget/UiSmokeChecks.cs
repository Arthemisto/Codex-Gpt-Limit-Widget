using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LimitWidget;

/// <summary>Opt-in UI validation with sample preferences, without modifying the user's settings or login.</summary>
internal static class UiSmokeChecks
{
    public static async Task RunAsync(MainWindow window, string outputDirectory)
    {
        var results = new List<string>();
        string? error = null;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (window.Busy && DateTime.UtcNow < deadline) await Task.Delay(100);
            if (!window.HasLiveQuota) throw new InvalidOperationException("Live quota was not available during smoke test.");
            results.Add("Live quota displayed");
            if (!window.UpdatedLabel.Text.StartsWith("Updated ")) throw new InvalidOperationException("Successful-update timestamp missing.");
            string timestamp = window.UpdatedLabel.Text;
            foreach (string theme in new[] { "Dark", "Light", "Adaptive glass" })
            {
                window.Theme = theme; window.RefreshView();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture((FrameworkElement)window.Content, Path.Combine(outputDirectory, $"widget-{theme.Replace(' ', '-')}.png"));
                if (window.UpdatedLabel.Text != timestamp) throw new InvalidOperationException("Appearance changed the update timestamp.");
            }
            results.Add("Three themes render without changing timestamp");
            foreach (double width in new[] { 300d, 340d, 390d })
            {
                window.SetWidgetSize(width); window.UpdateLayout();
                if (Math.Abs(window.ActualWidth - width) > 1) throw new InvalidOperationException("Widget size mismatch.");
            }
            window.SetWidgetSize(340); results.Add("Three widget sizes");
            window.Left = -100000; window.Top = -100000;
            WindowsIntegration.EnsureVisible(window);
            if (window.Left < -90000 || window.Top < -90000) throw new InvalidOperationException("Off-screen recovery failed.");
            results.Add("Off-screen window recovery");
            var settings = new SettingsWindow(window) { Owner = window }; settings.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Capture(settings, Path.Combine(outputDirectory, "settings.png")); settings.Close();
            results.Add("Settings open and render");
            var colorPicker = new ColorPickerWindow("Codex accent", window.AccentColor) { Owner = window }; colorPicker.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Capture(colorPicker, Path.Combine(outputDirectory, "color-picker.png")); colorPicker.Close();
            results.Add("RGB / HSV color picker opens and renders");
            window.Hide(); window.ShowWidget(); results.Add("Hide and restore");
        }
        catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        finally
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "smoke-result.json"), JsonSerializer.Serialize(new { passed = error == null, results, error }, new JsonSerializerOptions { WriteIndented = true }));
            window.ExitWidget();
        }
    }

    static void Capture(FrameworkElement visual, string path)
    {
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LimitWidget;

public sealed class SettingsWindow : Window
{
    readonly MainWindow widget;
    readonly StackPanel panel = new() { Margin = new Thickness(16, 10, 16, 12) };
    readonly TextBlock accountStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock loginMessage = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
    readonly List<Button> accountButtons = new();
    readonly Button cancelLogin = new() { Content = "Cancel sign-in" };
    readonly TextBlock saveStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    readonly CheckBox startup = new();
    readonly CheckBox remember;
    readonly CheckBox topmost;
    readonly CheckBox locked;

    public SettingsWindow(MainWindow widget)
    {
        this.widget = widget; Title = "Settings · Codex Limit Widget"; Width = 390; Height = 650; MinWidth = 370; MinHeight = 400;
        Resources.Add(typeof(CheckBox), new Style(typeof(CheckBox), (Style)Application.Current.FindResource(typeof(CheckBox))) { Setters = { new Setter(MarginProperty, new Thickness(0, 4, 0, 4)), new Setter(FontSizeProperty, 13d) } });
        FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "Foreground");
        // Settings stays opaque even when the desktop widget is translucent.
        Background = new SolidColorBrush(widget.Theme == "Light" || widget.Theme == "Adaptive glass" && WindowsIntegration.SystemLightTheme() ? Color.FromRgb(244, 247, 250) : Color.FromRgb(32, 40, 47));
        var root = new DockPanel();
        var footer = new StackPanel { Margin = new Thickness(16, 8, 16, 12) };
        var save = new Button { Content = "Save settings" };
        save.Click += (_, _) =>
        {
            try { WindowsIntegration.SetStartup(startup.IsChecked == true); widget.SaveSettings(); saveStatus.Text = "Saved"; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { saveStatus.Text = "Could not save settings. Check Windows permissions and try again."; }
        };
        footer.Children.Add(save); footer.Children.Add(saveStatus); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        Heading("Settings", 22); Heading("Refresh interval");
        int[] intervals = { 1, 5, 10, 15, 30, 60 };
        var intervalLabel = new TextBlock { Text = $"Every {widget.RefreshMinutes} minutes" }; panel.Children.Add(intervalLabel);
        var interval = new Slider { Minimum = 0, Maximum = 5, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight, Value = Array.IndexOf(intervals, widget.RefreshMinutes), Margin = new Thickness(0, 8, 0, 0) };
        interval.ValueChanged += (_, _) => { widget.RefreshMinutes = intervals[(int)interval.Value]; intervalLabel.Text = $"Every {widget.RefreshMinutes} minutes"; }; panel.Children.Add(interval);
        var ticks = new Grid(); for (int i = 0; i < intervals.Length; i++) { ticks.ColumnDefinitions.Add(new ColumnDefinition()); var label = new TextBlock { Text = $"{intervals[i]}m", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(label, i); ticks.Children.Add(label); } panel.Children.Add(ticks);
        Heading("Accent palette"); var colors = Row();
        foreach (string hex in new[] { "#39DFB5", "#318CFF", "#AB55F5", "#FFC44D" })
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var button = new Button { Width = 44, Height = 30, Background = new SolidColorBrush(color), ToolTip = hex };
            button.Click += (_, _) => { widget.AccentColor = color; widget.RefreshView(); }; colors.Children.Add(button);
        }
        Heading("Show"); Check("Codex limits", widget.ShowCodex, value => { widget.ShowCodex = value; widget.RefreshView(); });
        panel.Children.Add(new CheckBox { Content = "ChatGPT limits · coming in V2", IsEnabled = false, ToolTip = "ChatGPT chat quotas require a separate integration." });
        Check("Reset countdown", widget.ShowCountdown, value => { widget.ShowCountdown = value; widget.RefreshView(); });
        Check("Last updated", widget.ShowUpdated, value => { widget.ShowUpdated = value; widget.RefreshView(); });
        Check("Launch button", widget.ShowLaunch, value => { widget.ShowLaunch = value; widget.RefreshView(); });
        Heading("Widget size"); var sizes = Row();
        foreach (var size in new[] { ("S", 300d), ("M", 340d), ("L", 390d) })
        {
            var button = new Button { Content = size.Item1, Width = 90 }; button.Click += (_, _) => widget.SetWidgetSize(size.Item2); sizes.Children.Add(button);
        }
        Heading("Account"); panel.Children.Add(accountStatus); panel.Children.Add(loginMessage);
        AccountButton("Use existing Codex login", widget.UseExistingAsync);
        AccountButton("Sign in to ChatGPT", widget.SignInAsync);
        Note("To use Google, choose Continue with Google on the official sign-in page.");
        AccountButton("Disconnect widget", widget.DisconnectAsync);
        cancelLogin.Click += (_, _) => widget.CancelSignIn(); panel.Children.Add(cancelLogin);
        remember = Check("Remember next sign-in", widget.RememberMe, value => widget.RememberMe = value);
        Note("Browser sign-in is isolated from Codex. Remember uses Windows Credential Manager; off keeps the new login in memory. Existing Codex login uses Codex's own storage.");
        Heading("Theme"); var themes = Row();
        foreach (string theme in new[] { "Light", "Dark", "Adaptive glass" })
        {
            var button = new Button { Content = theme, Padding = new Thickness(9, 8, 9, 8) }; button.Click += (_, _) => { widget.Theme = theme; widget.RefreshView(); }; themes.Children.Add(button);
        }
        Heading("Glass background opacity");
        var opacityLabel = new TextBlock { Text = $"{widget.GlassOpacity:P0} · Adaptive glass only" }; panel.Children.Add(opacityLabel);
        var opacity = new Slider { Minimum = 20, Maximum = 100, Value = widget.GlassOpacity * 100, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 0) };
        opacity.ValueChanged += (_, _) => { widget.GlassOpacity = opacity.Value / 100; opacityLabel.Text = $"{widget.GlassOpacity:P0} · Adaptive glass only"; widget.RefreshView(); }; panel.Children.Add(opacity);
        Note("Transparent background; colors follow the Windows light/dark setting. Text stays opaque.");
        Heading("Windows");
        startup = Check("Launch at startup", WindowsIntegration.IsStartupEnabled(), _ => { });
        topmost = Check("Always on top", widget.Topmost, value => { widget.Topmost = value; widget.RefreshView(); });
        locked = Check("Lock position", widget.Locked, value => { widget.Locked = value; widget.RefreshView(); });
        var exit = new Button { Content = "Exit widget" }; exit.Click += (_, _) => widget.ExitWidget(); panel.Children.Add(exit);
        Note("× hides the widget to tray. Appearance previews immediately; Save keeps it for next launch.");
        widget.WidgetStateChanged += UpdateAccount;
        Closed += (_, _) => widget.WidgetStateChanged -= UpdateAccount;
        UpdateAccount();
    }

    void UpdateAccount()
    {
        accountStatus.Text = widget.AccountStatus;
        loginMessage.Text = widget.LoginMessage;
        loginMessage.Visibility = string.IsNullOrEmpty(widget.LoginMessage) ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in accountButtons) button.IsEnabled = !widget.Busy;
        cancelLogin.Visibility = widget.LoginPending ? Visibility.Visible : Visibility.Collapsed;
        remember.IsEnabled = !widget.Busy;
        if (topmost.IsChecked != widget.Topmost) topmost.IsChecked = widget.Topmost;
        if (locked.IsChecked != widget.Locked) locked.IsChecked = widget.Locked;
        Background = new SolidColorBrush(widget.Theme == "Light" || widget.Theme == "Adaptive glass" && WindowsIntegration.SystemLightTheme() ? Color.FromRgb(244, 247, 250) : Color.FromRgb(32, 40, 47));
    }
    void AccountButton(string title, Func<Task> action)
    {
        var button = new Button { Content = title }; button.Click += async (_, _) => await action(); accountButtons.Add(button); panel.Children.Add(button);
    }
    void Heading(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 6) });
    void Note(string text) { var note = new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 2) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(note); }
    StackPanel Row() { var row = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(row); return row; }
    CheckBox Check(string label, bool initial, Action<bool> action)
    {
        var box = new CheckBox { Content = label, IsChecked = initial }; box.Checked += (_, _) => action(true); box.Unchecked += (_, _) => action(false); panel.Children.Add(box); return box;
    }
}

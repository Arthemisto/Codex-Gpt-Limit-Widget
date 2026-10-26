using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LimitWidget;

public sealed class SettingsWindow : Window
{
    readonly MainWindow widget;
    readonly StackPanel panel = new() { Margin = new Thickness(12, 8, 12, 8) };
    readonly TextBlock accountStatus = new() { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock loginMessage = new() { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
    readonly Border loginNotice = new() { Background = new SolidColorBrush(Color.FromRgb(221, 229, 236)), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 5, 7, 5), Margin = new Thickness(0, 3, 0, 3) };
    readonly TextBlock tokenValue = new() { FontSize = 18, FontWeight = FontWeights.Bold, Text = "—" };
    readonly TextBlock tokenCaption = new() { FontSize = 11, Text = "Lifetime · account service" };
    readonly List<Button> accountButtons = new();
    readonly Button cancelLogin = new() { Content = "Cancel sign-in" };
    readonly TextBlock saveStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    readonly CheckBox startup = new();
    readonly CheckBox remember;
    readonly CheckBox topmost;
    readonly CheckBox locked;
    readonly StackPanel glassOptions = new();

    public SettingsWindow(MainWindow widget)
    {
        this.widget = widget; Icon = widget.Icon; Title = "Settings · Codex Limit Widget"; Width = 390; Height = 650; MinWidth = 370; MinHeight = 400;
        Resources.Add(typeof(CheckBox), new Style(typeof(CheckBox), (Style)Application.Current.FindResource(typeof(CheckBox))) { Setters = { new Setter(MarginProperty, new Thickness(0, 3, 0, 3)), new Setter(FontSizeProperty, 14d) } });
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "Foreground");
        // Settings stays opaque even when the desktop widget is translucent.
        UpdateBackground();
        var root = new DockPanel();
        var footer = new StackPanel { Margin = new Thickness(12, 4, 12, 8) };
        var save = new Button { Content = "Save settings" };
        save.Click += (_, _) =>
        {
            try { WindowsIntegration.SetStartup(startup.IsChecked == true); widget.SaveSettings(); saveStatus.Text = "Saved"; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { saveStatus.Text = "Could not save settings. Check Windows permissions and try again."; }
        };
        footer.Children.Add(save); footer.Children.Add(saveStatus); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "Settings", FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 15) });
        heading.Children.Add(new TextBlock { Text = "Refresh interval", FontSize = 14, FontWeight = FontWeights.SemiBold }); header.Children.Add(heading);
        var usage = new StackPanel();
        usage.Children.Add(new TextBlock { Text = "Total tokens used", FontSize = 13, FontWeight = FontWeights.SemiBold }); usage.Children.Add(tokenValue); usage.Children.Add(tokenCaption);
        var tokenCard = new Border { Child = usage, CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(5, 0, 0, 0) };
        tokenCard.SetResourceReference(BackgroundProperty, "ButtonSurface"); Grid.SetColumn(tokenCard, 1); header.Children.Add(tokenCard); panel.Children.Add(header);
        int[] intervals = { 1, 5, 10, 15, 30, 60 };
        var intervalLabel = new TextBlock { Text = $"Every {widget.RefreshMinutes} minutes" }; panel.Children.Add(intervalLabel);
        var interval = new Slider { Minimum = 0, Maximum = 5, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight, Value = Array.IndexOf(intervals, widget.RefreshMinutes), Margin = new Thickness(0, 8, 0, 0) };
        interval.ValueChanged += (_, _) => { widget.RefreshMinutes = intervals[(int)interval.Value]; intervalLabel.Text = $"Every {widget.RefreshMinutes} minutes"; }; panel.Children.Add(interval);
        var ticks = new Grid(); for (int i = 0; i < intervals.Length; i++) { ticks.ColumnDefinitions.Add(new ColumnDefinition()); var label = new TextBlock { Text = $"{intervals[i]}m", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(label, i); ticks.Children.Add(label); } panel.Children.Add(ticks);
        Heading("Accent color"); var colors = new UniformGrid { Columns = 1 }; panel.Children.Add(colors);
        AddColorPicker(colors, "Codex", () => widget.AccentColor, color => widget.AccentColor = color);
        Heading("Show"); var visibleOptions = new UniformGrid { Columns = 2 }; panel.Children.Add(visibleOptions);
        Check("Codex limits", widget.ShowCodex, value => { widget.ShowCodex = value; widget.RefreshView(); }, visibleOptions);
        Check("Reset countdown", widget.ShowCountdown, value => { widget.ShowCountdown = value; widget.RefreshView(); }, visibleOptions);
        Check("Last updated", widget.ShowUpdated, value => { widget.ShowUpdated = value; widget.RefreshView(); }, visibleOptions);
        Check("Launch button", widget.ShowLaunch, value => { widget.ShowLaunch = value; widget.RefreshView(); }, visibleOptions);
        Heading("Widget size"); var sizes = new UniformGrid { Columns = 3 }; panel.Children.Add(sizes);
        foreach (var size in new[] { ("S", 300d), ("M", 340d), ("L", 390d) })
        {
            var button = new Button { Content = size.Item1 }; button.Click += (_, _) => widget.SetWidgetSize(size.Item2); sizes.Children.Add(button);
        }
        Heading("Account"); panel.Children.Add(new Border { Child = accountStatus, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromRgb(221, 229, 236)), Padding = new Thickness(7, 5, 7, 5) });
        loginNotice.Child = loginMessage; panel.Children.Add(loginNotice);
        var signIn = AccountButton("Sign in to ChatGPT", widget.SignInAsync); signIn.SetResourceReference(BackgroundProperty, "Accent"); signIn.Foreground = Brushes.Black; signIn.FontWeight = FontWeights.Bold;
        var connectionActions = new UniformGrid { Columns = 2 }; panel.Children.Add(connectionActions);
        AccountButton("Use Codex login", widget.UseExistingAsync, connectionActions);
        AccountButton("Disconnect", widget.DisconnectAsync, connectionActions);
        cancelLogin.Click += (_, _) => widget.CancelSignIn(); panel.Children.Add(cancelLogin);
        remember = Check("Remember next sign-in", widget.RememberMe, value => widget.RememberMe = value);
        Note("Google is available on the browser sign-in page. Remember saves the widget login securely.");
        remember.ToolTip = "Applies to the next browser sign-in. On: Windows Credential Manager. Off: session memory only. Existing Codex login uses its own storage.";
        Heading("Theme"); var themes = new UniformGrid { Columns = 3 }; panel.Children.Add(themes);
        foreach (string theme in new[] { "Light", "Dark", "Adaptive glass" })
        {
            var button = new Button { Content = theme, Padding = new Thickness(9, 8, 9, 8) }; button.Click += (_, _) => { widget.Theme = theme; widget.RefreshView(); }; themes.Children.Add(button);
        }
        panel.Children.Add(glassOptions);
        glassOptions.Children.Add(new TextBlock { Text = "Glass background opacity", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
        var opacityLabel = new TextBlock { Text = $"{widget.GlassOpacity:P0}" }; glassOptions.Children.Add(opacityLabel);
        var opacity = new Slider { Minimum = 20, Maximum = 100, Value = widget.GlassOpacity * 100, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 0) };
        opacity.ValueChanged += (_, _) => { widget.GlassOpacity = opacity.Value / 100; opacityLabel.Text = $"{widget.GlassOpacity:P0}"; widget.RefreshView(); }; glassOptions.Children.Add(opacity);
        Heading("Windows");
        startup = Check("Launch at startup", WindowsIntegration.IsStartupEnabled(), _ => { });
        topmost = Check("Always on top", widget.Topmost, value => { widget.Topmost = value; widget.RefreshView(); });
        locked = Check("Lock position", widget.Locked, value => { widget.Locked = value; widget.RefreshView(); });
        var exit = new Button { Content = "Exit widget" }; exit.Click += (_, _) => widget.ExitWidget(); panel.Children.Add(exit);
        Note("× hides to tray. Save keeps changes for next launch.");
        widget.WidgetStateChanged += UpdateAccount;
        Closed += (_, _) => widget.WidgetStateChanged -= UpdateAccount;
        UpdateAccount();
    }

    void UpdateAccount()
    {
        glassOptions.Visibility = widget.Theme == "Adaptive glass" ? Visibility.Visible : Visibility.Collapsed;
        accountStatus.Text = widget.AccountStatus;
        tokenValue.Text = widget.TokenUsage?.DisplayValue ?? "—";
        tokenCaption.Text = widget.UsageUnavailable ? "Currently unavailable" : "Lifetime · account service";
        tokenValue.ToolTip = widget.TokenUsage?.LifetimeTokens is { } total ? $"{total:N0} lifetime tokens reported by the Codex account service. Updated {widget.UsageUpdatedAt:HH:mm}." : "The account service has not returned a lifetime token count.";
        loginMessage.Text = widget.LoginMessage;
        loginNotice.Visibility = string.IsNullOrEmpty(widget.LoginMessage) ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in accountButtons) button.IsEnabled = !widget.Busy;
        cancelLogin.Visibility = widget.LoginPending ? Visibility.Visible : Visibility.Collapsed;
        remember.IsEnabled = !widget.Busy;
        if (topmost.IsChecked != widget.Topmost) topmost.IsChecked = widget.Topmost;
        if (locked.IsChecked != widget.Locked) locked.IsChecked = widget.Locked;
        UpdateBackground();
    }
    void UpdateBackground()
    {
        Color color = widget.Theme == "Light" || widget.Theme == "Adaptive glass" && WindowsIntegration.SystemLightTheme()
            ? Color.FromRgb(244, 247, 250) : Color.FromRgb(32, 40, 47);
        if (Background is SolidColorBrush existing && existing.Color == color) return;
        var brush = new SolidColorBrush(color); brush.Freeze(); Background = brush;
    }
    Button AccountButton(string title, Func<Task> action, Panel? destination = null)
    {
        var button = new Button { Content = title }; button.Click += async (_, _) => await action(); accountButtons.Add(button); (destination ?? panel).Children.Add(button); return button;
    }
    void AddColorPicker(Panel parent, string name, Func<Color> getColor, Action<Color> setColor)
    {
        var swatch = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(getColor()), Margin = new Thickness(0, 0, 7, 0) };
        var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(swatch); content.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, ToolTip = "Choose hue, saturation, brightness or RGB / HEX" };
        button.Click += (_, _) =>
        {
            var picker = new ColorPickerWindow($"{name} accent", getColor()) { Owner = this };
            if (picker.ShowDialog() == true) { setColor(picker.SelectedColor); swatch.Background = new SolidColorBrush(picker.SelectedColor); widget.RefreshView(); }
        };
        parent.Children.Add(button);
    }
    void Heading(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 4) });
    void Note(string text) { var note = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 2) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(note); }
    CheckBox Check(string label, bool initial, Action<bool> action, Panel? destination = null)
    {
        var box = new CheckBox { Content = label, IsChecked = initial }; box.Checked += (_, _) => action(true); box.Unchecked += (_, _) => action(false); (destination ?? panel).Children.Add(box); return box;
    }
}

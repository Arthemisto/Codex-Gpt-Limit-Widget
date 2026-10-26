using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LimitWidget.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LimitWidget;

public partial class MainWindow : Window
{
    SettingsWindow? settings;
    CodexClient? client;
    QuotaSnapshot? snapshot;
    readonly CancellationTokenSource lifetime = new();
    CancellationTokenSource? loginCancellation;
    readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly Stopwatch sinceAttempt = new();
    readonly List<(TextBlock Label, DateTimeOffset? Reset)> countdowns = new();
    readonly Forms.NotifyIcon tray;
    readonly Forms.ContextMenuStrip menu;
    readonly System.Drawing.Icon trayImage;
    bool busy, exiting, hasError;
    readonly bool smokeTest;
    string status = "Connecting to Codex…";
    public event Action? WidgetStateChanged;
    public bool ShowCodex = true, Locked, RememberMe = true, ShowUpdated = true, ShowCountdown = true, ShowLaunch = true;
    public string Theme = "Dark", SessionMode = "existing";
    public Color AccentColor = Color.FromRgb(57, 223, 181);
    public double GlassOpacity = 0.8;
    public int RefreshMinutes = 10;
    public bool Busy => busy;
    public bool LoginPending => loginCancellation != null;
    public string AccountStatus { get; private set; } = "Connecting…";
    public string LoginMessage { get; private set; } = "";
    public UsageSummary? TokenUsage { get; private set; }
    public DateTimeOffset? UsageUpdatedAt { get; private set; }
    public bool UsageUnavailable { get; private set; }
    public bool OwnSession => SessionMode == "widget";
    internal bool HasLiveQuota => snapshot != null;

    public MainWindow(bool smokeTest = false)
    {
        this.smokeTest = smokeTest;
        InitializeComponent();
        SourceInitialized += (_, _) => WindowsIntegration.HideFromTaskSwitcher(this);
        var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        var saved = smokeTest ? new WidgetSettings() : WidgetSettings.Load();
        ShowCodex = saved.ShowCodex; Locked = saved.Locked; Topmost = saved.Topmost;
        RememberMe = saved.RememberMe; ShowUpdated = saved.ShowUpdated; ShowCountdown = saved.ShowCountdown; ShowLaunch = saved.ShowLaunch;
        SessionMode = saved.SessionMode == "widget" ? "widget" : "existing";
        Theme = saved.Theme is "Light" or "Dark" or "Adaptive glass" ? saved.Theme : "Dark";
        try { if (!string.IsNullOrWhiteSpace(saved.Accent)) AccentColor = (Color)ColorConverter.ConvertFromString(saved.Accent); }
        catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException) { }
        GlassOpacity = double.IsFinite(saved.Opacity) ? Math.Clamp(saved.Opacity, 0.2, 1) : 0.8;
        RefreshMinutes = new[] { 1, 5, 10, 15, 30, 60 }.Contains(saved.RefreshMinutes) ? saved.RefreshMinutes : 10;
        SetWidgetSize(double.IsFinite(saved.Width) ? Math.Clamp(saved.Width, 300, 390) : 340);
        if (!smokeTest && WindowPlacement.Load() is { } position)
        {
            WindowStartupLocation = WindowStartupLocation.Manual; Left = position.Left; Top = position.Top;
        }

        menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show widget", null, (_, _) => Dispatcher.Invoke(ShowWidget));
        menu.Items.Add("Refresh", null, async (_, _) => await RefreshLimitsAsync());
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitWidget));
        using (var iconStream = Application.GetResourceStream(iconUri)!.Stream)
        using (var embeddedIcon = new System.Drawing.Icon(iconStream))
            trayImage = new System.Drawing.Icon(embeddedIcon, Forms.SystemInformation.SmallIconSize);
        tray = new Forms.NotifyIcon { Icon = trayImage, Text = "Codex Limit Widget", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidget);

        updateTimer.Tick += async (_, _) =>
        {
            UpdateCountdowns(); UpdateStatus();
            if (!busy && sinceAttempt.Elapsed >= TimeSpan.FromMinutes(hasError ? Math.Min(RefreshMinutes, 2) : RefreshMinutes)) await RefreshLimitsAsync();
        };
        Loaded += async (_, _) => { WindowsIntegration.EnsureVisible(this); updateTimer.Start(); await RefreshLimitsAsync(); };
        SizeChanged += (_, _) => { if (IsLoaded) WindowsIntegration.EnsureVisible(this); };
        SystemEvents.UserPreferenceChanged += OnThemeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            updateTimer.Stop(); lifetime.Cancel(); loginCancellation?.Cancel(); client?.Dispose();
            SystemEvents.UserPreferenceChanged -= OnThemeChanged; SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
            tray.Visible = false; tray.Dispose(); menu.Dispose(); trayImage.Dispose();
        };
        RefreshView();
    }

    public void SetWidgetSize(double width)
    {
        Width = width;
        ((FrameworkElement)Content).LayoutTransform = new ScaleTransform(width / 340, width / 340);
    }

    public void SaveSettings() => new WidgetSettings
    {
        ShowCodex = ShowCodex, Locked = Locked, Topmost = Topmost, Theme = Theme,
        Accent = AccentColor.ToString(), Width = Width, Opacity = GlassOpacity, RefreshMinutes = RefreshMinutes,
        RememberMe = RememberMe, SessionMode = SessionMode, ShowUpdated = ShowUpdated, ShowCountdown = ShowCountdown, ShowLaunch = ShowLaunch
    }.Save();

    static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
    }

    public void RefreshView()
    {
        bool light = Theme == "Light" || Theme == "Adaptive glass" && WindowsIntegration.SystemLightTheme();
        byte alpha = Theme == "Adaptive glass" ? (byte)Math.Round(GlassOpacity * 255) : (byte)255;
        Application.Current.Resources["Surface"] = FrozenBrush(light ? Color.FromArgb(alpha, 244, 247, 250) : Color.FromArgb(alpha, 32, 40, 47));
        Application.Current.Resources["Foreground"] = FrozenBrush(light ? Color.FromRgb(25, 36, 44) : Color.FromRgb(244, 247, 250));
        Application.Current.Resources["Muted"] = FrozenBrush(light ? Color.FromRgb(79, 96, 110) : Color.FromRgb(168, 181, 192));
        Application.Current.Resources["Line"] = FrozenBrush(light ? Color.FromRgb(202, 211, 219) : Color.FromRgb(70, 82, 91));
        Application.Current.Resources["Accent"] = FrozenBrush(AccentColor);
        Application.Current.Resources["ButtonSurface"] = FrozenBrush(light ? Color.FromRgb(221, 229, 236) : Color.FromRgb(48, 60, 70));
        QuotaRows.Children.Clear(); countdowns.Clear();
        if (ShowCodex) AddService();
        EmptyHint.Visibility = ShowCodex ? Visibility.Collapsed : Visibility.Visible;
        LaunchButton.Visibility = ShowLaunch ? Visibility.Visible : Visibility.Collapsed;
        LaunchDivider.Visibility = LaunchButton.Visibility;
        UpdatedLabel.Visibility = ShowUpdated ? Visibility.Visible : Visibility.Collapsed;
        LockButton.Content = Locked ? "🔒 Locked" : "🔓 Move";
        TopButton.Content = Topmost ? "↑ On top" : "↓ On top";
        TopButton.Foreground = Brushes.Black;
        TopButton.Background = Topmost ? FrozenBrush(AccentColor) : FrozenBrush(Color.FromRgb(221, 229, 236));
        UpdateCountdowns(); UpdateStatus();
        WidgetStateChanged?.Invoke();
    }

    void UpdateStatus()
    {
        bool stale = snapshot != null && (hasError || DateTimeOffset.Now - snapshot.UpdatedAt > TimeSpan.FromMinutes(RefreshMinutes * 2));
        ConnectionLabel.Text = LoginPending ? "Signing in" : busy ? "Updating" : stale ? "Stale data" : snapshot != null ? "Signed in" : "Not connected";
        ConnectionBadge.Background = snapshot != null && !stale ? FrozenBrush(AccentColor) : FrozenBrush(Color.FromRgb(221, 229, 236));
        UpdatedLabel.Text = snapshot == null ? "Not updated yet" : $"Updated {snapshot.UpdatedAt:HH:mm}{(stale ? " · stale" : "")}";
        UpdatedLabel.ToolTip = snapshot == null ? "No successful quota request yet" : $"Last successful request: {snapshot.UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        StatusLabel.Text = status; StatusLabel.Visibility = string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible;
        RefreshButton.IsEnabled = !busy;
        tray.Text = snapshot?.Primary is { } first ? $"Codex · {first.RemainingPercent:0}% remaining{(stale ? " · stale" : "")}" : "Codex Limit Widget · not connected";
    }

    async Task EnsureClientAsync()
    {
        if (client?.IsRunning == true) return;
        client?.Dispose(); client = new CodexClient();
        await client.StartAsync(SessionMode == "existing", RememberMe, lifetime.Token);
    }

    public async Task RefreshLimitsAsync()
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true; sinceAttempt.Restart(); UpdateStatus(); WidgetStateChanged?.Invoke();
        try
        {
            await EnsureClientAsync();
            if (!await client!.HasAccountAsync(lifetime.Token))
            {
                snapshot = null; TokenUsage = null; UsageUpdatedAt = null; AccountStatus = "Not signed in";
                throw new IOException("Sign in from Settings or use an existing Codex login. API-key accounts do not expose subscription limits.");
            }
            AccountStatus = OwnSession ? "Widget account connected" : "Using existing Codex login";
            var result = await client.ReadLimitsAsync(lifetime.Token);
            snapshot = result; hasError = false; status = "";
            // Optional statistics must never prevent a successful quota refresh.
            try
            {
                using var usageTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                usageTimeout.CancelAfter(TimeSpan.FromSeconds(8));
                TokenUsage = await client.ReadUsageAsync(usageTimeout.Token);
                UsageUpdatedAt = DateTimeOffset.Now; UsageUnavailable = TokenUsage.LifetimeTokens == null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException or TimeoutException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException)
            { TokenUsage = null; UsageUpdatedAt = null; UsageUnavailable = true; }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            hasError = true;
            status = e is TimeoutException ? "Request timed out. Retrying automatically." : e is FileNotFoundException ? "Codex CLI not found. Install Codex, then refresh." : e is IOException ? e.Message : "Could not connect to Codex. Check your login and connection, then refresh.";
        }
        finally
        {
            // Persistent sessions can be reopened next poll; keep only ephemeral login alive.
            if (client is { SharedSession: true } or { PersistentSession: true }) { client.Dispose(); client = null; }
            busy = false; if (!lifetime.IsCancellationRequested) RefreshView();
        }
    }

    public async Task SignInAsync()
    {
        if (busy) return;
        busy = true;
        LoginMessage = "Opening ChatGPT sign-in… Choose Continue with Google in your browser.";
        loginCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var attempt = new CodexClient();
        bool signedIn = false;
        try
        {
            RefreshView();
            await attempt.StartAsync(false, RememberMe, loginCancellation.Token);
            await attempt.SignInAsync(uri =>
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                LoginMessage = "Waiting for ChatGPT / Google sign-in in your browser…";
                WidgetStateChanged?.Invoke();
            }, loginCancellation.Token);
            if (!await attempt.HasAccountAsync(loginCancellation.Token)) throw new IOException("The browser flow did not establish a ChatGPT session.");
            client?.Dispose(); client = attempt; SessionMode = "widget"; snapshot = null; TokenUsage = null; UsageUpdatedAt = null; signedIn = true;
            AccountStatus = "ChatGPT account connected · widget session";
            LoginMessage = RememberMe ? "Signed in. Login is saved in Windows Credential Manager." : "Signed in for this session only.";
            try { SaveSettings(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LoginMessage += " Preferences could not be saved; use Save settings."; }
            status = "";
        }
        catch (OperationCanceledException) { LoginMessage = "Sign-in cancelled. Previous connection was kept."; }
        catch (TimeoutException) { LoginMessage = "Sign-in timed out. Please try again."; }
        catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException)
        { LoginMessage = "Sign-in failed. Check your browser and retry. Remember me requires Windows Credential Manager."; }
        finally
        {
            if (!signedIn) attempt.Dispose();
            loginCancellation.Dispose(); loginCancellation = null; busy = false;
            if (!lifetime.IsCancellationRequested) { RefreshView(); if (signedIn) await RefreshLimitsAsync(); }
        }
    }

    public void CancelSignIn() => loginCancellation?.Cancel();

    public async Task UseExistingAsync()
    {
        if (busy) return;
        client?.Dispose(); client = null; snapshot = null; TokenUsage = null; UsageUpdatedAt = null; LoginMessage = ""; SessionMode = "existing";
        await RefreshLimitsAsync();
    }

    public async Task DisconnectAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            bool persistentCleared = client is { IsRunning: true, SharedSession: false, PersistentSession: true };
            if (client?.IsRunning == true && !client.SharedSession) await client.SignOutAsync(lifetime.Token);
            if (!persistentCleared)
            {
                using var remembered = new CodexClient();
                await remembered.StartAsync(false, true, lifetime.Token);
                await remembered.SignOutAsync(lifetime.Token);
            }
            client?.Dispose(); client = null; snapshot = null; TokenUsage = null; UsageUpdatedAt = null; LoginMessage = ""; SessionMode = "widget";
            AccountStatus = "Disconnected"; status = "Sign in from Settings to connect your widget."; hasError = false;
            SaveSettings();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException or TimeoutException or UnauthorizedAccessException or OperationCanceledException)
        { status = "Could not disconnect. Please try again."; }
        finally { busy = false; RefreshView(); }
    }

    void UpdateCountdowns()
    {
        foreach (var (label, reset) in countdowns)
        {
            if (reset == null) { label.Text = "Reset unavailable"; continue; }
            var remaining = reset.Value - DateTimeOffset.UtcNow;
            label.Text = remaining <= TimeSpan.Zero ? "Reset due · refresh" : remaining.TotalDays >= 1 ? $"Resets in {(int)remaining.TotalDays}d {remaining.Hours:00}h" : $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes:00}m";
        }
    }

    void AddService()
    {
        var section = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        section.Children.Add(new TextBlock { Text = "CODEX", FontSize = 12, Margin = new Thickness(2, 0, 0, 10) });
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var windows = new[] { snapshot?.Primary, snapshot?.Secondary };
        for (int i = 0; i < windows.Length; i++)
        {
            var window = windows[i];
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            cell.Children.Add(new QuotaRing { Width = 112, Height = 112, Percent = window?.RemainingPercent, RingBrush = FrozenBrush(AccentColor) });
            cell.Children.Add(new TextBlock { Text = window?.Label ?? (i == 0 ? "Current window" : "Long window"), FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            if (ShowCountdown)
            {
                var reset = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
                reset.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); cell.Children.Add(reset);
                countdowns.Add((reset, window?.ResetsAt));
            }
            Grid.SetColumn(cell, i); grid.Children.Add(cell);
        }
        section.Children.Add(grid); QuotaRows.Children.Add(section);
    }

    void DragHeader(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is Button) return;
            source = VisualTreeHelper.GetParent(source);
        }
        if (Locked || e.ButtonState != MouseButtonState.Pressed) return;
        DragMove(); WindowsIntegration.EnsureVisible(this); new WindowPlacement(Left, Top).Save();
    }
    void ToggleLock(object sender, RoutedEventArgs e) { Locked = !Locked; RefreshView(); }
    void ToggleTop(object sender, RoutedEventArgs e) { Topmost = !Topmost; RefreshView(); }
    async void RefreshClicked(object sender, RoutedEventArgs e) => await RefreshLimitsAsync();
    void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();
    public void ShowSettings()
    {
        ShowWidget();
        if (settings != null) { settings.Activate(); return; }
        settings = new SettingsWindow(this) { Owner = this }; settings.Closed += (_, _) => settings = null; settings.Show();
    }
    public void ShowWidget() { Show(); WindowState = WindowState.Normal; WindowsIntegration.EnsureVisible(this); Activate(); }
    void CloseWidget(object sender, RoutedEventArgs e) { settings?.Close(); Hide(); }
    public void ExitWidget() { exiting = true; Close(); }
    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; settings?.Close(); Hide(); }
        else if (!smokeTest) new WindowPlacement(Left, Top).Save();
    }
    void LaunchCodex(object sender, RoutedEventArgs e)
    {
        try { WindowsIntegration.LaunchCodex(); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show(this, "The Codex desktop app could not be opened. Install it or repair its codex:// association.", "Launch Codex", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    void OnThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { if (Theme == "Adaptive glass") RefreshView(); });
    void OnDisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => WindowsIntegration.EnsureVisible(this));
}

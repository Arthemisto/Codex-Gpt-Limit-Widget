using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace LimitWidget;
public partial class MainWindow : Window
{
 SettingsWindow? settings;
 public bool ShowCodex = true, ShowGpt, Locked;
 public string Theme = "Dark";
 public Color AccentColor = Color.FromRgb(57, 223, 181);
 public double GlassOpacity = 0.8;
 public int RefreshMinutes = 10;
 public MainWindow()
 {
  InitializeComponent();
  var saved = WidgetSettings.Load();
  ShowCodex = saved.ShowCodex; ShowGpt = saved.ShowGpt; Locked = saved.Locked; Topmost = saved.Topmost;
  Theme = saved.Theme is "Light" or "Dark" or "Adaptive glass" ? saved.Theme : "Dark";
  try { AccentColor = (Color)ColorConverter.ConvertFromString(saved.Accent); } catch (Exception e) when (e is FormatException or NotSupportedException) { }
  GlassOpacity = double.IsFinite(saved.Opacity) ? Math.Clamp(saved.Opacity, 0.2, 1) : 0.8;
  RefreshMinutes = new[] { 1, 5, 10, 15, 30, 60 }.Contains(saved.RefreshMinutes) ? saved.RefreshMinutes : 10;
  SetWidgetSize(double.IsFinite(saved.Width) ? Math.Clamp(saved.Width, 330, 450) : 390);
  RefreshView();
 }
 public void SetWidgetSize(double width)
 {
  Width = width;
  ((FrameworkElement)Content).LayoutTransform = new ScaleTransform(width / 390, width / 390);
 }
 public void SaveSettings() => new WidgetSettings { ShowCodex = ShowCodex, ShowGpt = ShowGpt, Locked = Locked, Topmost = Topmost, Theme = Theme, Accent = AccentColor.ToString(), Width = Width, Opacity = GlassOpacity, RefreshMinutes = RefreshMinutes }.Save();
 public void RefreshView()
 {
  bool light = Theme == "Light";
  Application.Current.Resources["Surface"] = new SolidColorBrush(light ? Color.FromRgb(244, 247, 250) : Theme == "Adaptive glass" ? Color.FromArgb((byte)Math.Round(GlassOpacity * 255), 32, 40, 47) : Color.FromRgb(32, 40, 47));
  Application.Current.Resources["Foreground"] = new SolidColorBrush(light ? Color.FromRgb(25, 36, 44) : Color.FromRgb(244, 247, 250));
  Application.Current.Resources["Muted"] = new SolidColorBrush(light ? Color.FromRgb(79, 96, 110) : Color.FromRgb(168, 181, 192));
  Application.Current.Resources["Line"] = new SolidColorBrush(light ? Color.FromRgb(202, 211, 219) : Color.FromRgb(70, 82, 91));
  Application.Current.Resources["Accent"] = new SolidColorBrush(AccentColor);
  QuotaRows.Children.Clear();
  if (ShowCodex) AddService("CODEX", 72, 46, new SolidColorBrush(AccentColor));
  if (ShowGpt) AddService("CHATGPT · V2 PREVIEW", 81, 63, new SolidColorBrush(Color.FromRgb(171, 85, 245)));
  EmptyHint.Visibility = ShowCodex || ShowGpt ? Visibility.Collapsed : Visibility.Visible;
  LockButton.Content = Locked ? "🔒 Locked" : "🔓 Move";
  TopButton.Content = Topmost ? "↑ On" : "↑ Off";
 }
 void AddService(string title, double first, double second, Brush color)
 {
  var section = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
  section.Children.Add(new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(2, 0, 0, 10) });
  var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
  for (int i = 0; i < 2; i++)
  {
   var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
   cell.Children.Add(new QuotaRing { Width = 136, Height = 136, Percent = i == 0 ? first : second, RingBrush = color });
   cell.Children.Add(new TextBlock { Text = i == 0 ? "Current window" : "Weekly", FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
   var reset = new TextBlock { Text = i == 0 ? "Resets in 2h 14m" : "Resets in 3d 08h", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
   reset.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); cell.Children.Add(reset);
   Grid.SetColumn(cell, i); grid.Children.Add(cell);
  }
  section.Children.Add(grid); QuotaRows.Children.Add(section);
 }
 void DragHeader(object sender, MouseButtonEventArgs e) { if (!Locked && e.OriginalSource is not Button) DragMove(); }
 void ToggleLock(object sender, RoutedEventArgs e) { Locked = !Locked; RefreshView(); }
 void ToggleTop(object sender, RoutedEventArgs e) { Topmost = !Topmost; RefreshView(); }
 void OpenSettings(object sender, RoutedEventArgs e)
 {
  if (settings != null) { settings.Activate(); return; }
  settings = new SettingsWindow(this) { Owner = this }; settings.Closed += (_, _) => settings = null; settings.Show();
 }
 void CloseWidget(object sender, RoutedEventArgs e) => Close();
 void LaunchCodex(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Codex launch will be connected in the next step. This prototype uses sample data only.", "Prototype", MessageBoxButton.OK, MessageBoxImage.Information);
}

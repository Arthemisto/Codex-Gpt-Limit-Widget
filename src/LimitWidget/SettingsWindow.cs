using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LimitWidget;
public sealed class SettingsWindow : Window
{
 readonly MainWindow widget;
 readonly StackPanel panel = new() { Margin = new Thickness(16, 10, 16, 12) };
 public SettingsWindow(MainWindow widget)
 {
  this.widget = widget; Title = "Settings — Prototype"; Width = 390; Height = 650; MinWidth = 370; MinHeight = 400;
  Resources.Add(typeof(CheckBox), new Style(typeof(CheckBox), (Style)Application.Current.FindResource(typeof(CheckBox))) { Setters = { new Setter(MarginProperty, new Thickness(0, 4, 0, 4)), new Setter(FontSizeProperty, 13d) } });
  FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
  SetResourceReference(BackgroundProperty, "Surface");
  Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
  Heading("Settings", 22); Heading("Refresh interval");
  int[] intervals = { 1, 5, 10, 15, 30, 60 };
  var intervalLabel = new TextBlock { Text = $"Every {widget.RefreshMinutes} minutes" }; panel.Children.Add(intervalLabel);
  var interval = new Slider { Minimum = 0, Maximum = 5, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight, Value = Array.IndexOf(intervals, widget.RefreshMinutes), Margin = new Thickness(0, 10, 0, 0) };
  interval.ValueChanged += (_, _) => { widget.RefreshMinutes = intervals[(int)interval.Value]; intervalLabel.Text = $"Every {widget.RefreshMinutes} minutes"; }; panel.Children.Add(interval);
  Note("1m          5m          10m          15m          30m          60m");
  Note("Preview only · no network requests");
  Heading("Accent palette"); var colors = Row();
  foreach (string hex in new[] { "#39DFB5", "#318CFF", "#AB55F5", "#FFC44D" })
  {
   var color = (Color)ColorConverter.ConvertFromString(hex);
   var button = new Button { Width = 44, Height = 34, Background = new SolidColorBrush(color), ToolTip = hex };
   button.Click += (_, _) => { widget.AccentColor = color; widget.RefreshView(); }; colors.Children.Add(button);
  }
  Heading("Show limits"); Check("Codex", widget.ShowCodex, value => { widget.ShowCodex = value; widget.RefreshView(); });
  Check("ChatGPT · V2 preview", widget.ShowGpt, value => { widget.ShowGpt = value; widget.RefreshView(); });
  Heading("Widget size"); var sizes = Row();
  foreach (var size in new[] { ("S", 300d), ("M", 340d), ("L", 390d) })
  {
   var button = new Button { Content = size.Item1, Width = 90 }; button.Click += (_, _) => widget.SetWidgetSize(size.Item2); sizes.Children.Add(button);
  }
  Heading("Account"); var login = new Button { Content = "Sign in · unavailable in prototype", IsEnabled = false }; panel.Children.Add(login);
  Check("Remember me", true, _ => { });
  var startup = new CheckBox { Content = "Launch at startup · next step", IsEnabled = false }; panel.Children.Add(startup);
  Heading("Theme"); var themes = Row();
  foreach (string theme in new[] { "Light", "Dark", "Adaptive glass" })
  {
   var button = new Button { Content = theme, Padding = new Thickness(9, 8, 9, 8) }; button.Click += (_, _) => { widget.Theme = theme; widget.RefreshView(); }; themes.Children.Add(button);
  }
  Heading("Glass background opacity");
  var opacityLabel = new TextBlock { Text = $"{widget.GlassOpacity:P0} · Adaptive glass only" }; panel.Children.Add(opacityLabel);
  var opacity = new Slider { Minimum = 20, Maximum = 100, Value = widget.GlassOpacity * 100, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 10, 0, 0) };
  opacity.ValueChanged += (_, _) => { widget.GlassOpacity = opacity.Value / 100; opacityLabel.Text = $"{widget.GlassOpacity:P0} · Adaptive glass only"; widget.RefreshView(); }; panel.Children.Add(opacity);
  Note("Text stays opaque. Native blur and automatic adaptation come later.");
  Check("Always on top", widget.Topmost, value => { widget.Topmost = value; widget.RefreshView(); });
  Check("Lock position", widget.Locked, value => { widget.Locked = value; widget.RefreshView(); });
  var save = new Button { Content = "Save settings", Margin = new Thickness(0, 16, 0, 0) };
  save.Click += (_, _) =>
  {
   try { widget.SaveSettings(); Close(); }
   catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { MessageBox.Show(this, $"Could not save settings: {e.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Warning); }
  };
  panel.Children.Add(save);
  Note("Changes preview immediately. Save keeps them for the next launch.");
 }
 void Heading(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 6) });
 void Note(string text) { var note = new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 2) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(note); }
 StackPanel Row() { var row = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(row); return row; }
 void Check(string label, bool initial, Action<bool> action) { var box = new CheckBox { Content = label, IsChecked = initial }; box.Checked += (_, _) => action(true); box.Unchecked += (_, _) => action(false); panel.Children.Add(box); }
}

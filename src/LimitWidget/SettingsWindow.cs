using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LimitWidget;
public sealed class SettingsWindow : Window
{
 readonly MainWindow widget;
 readonly StackPanel panel = new() { Margin = new Thickness(24) };
 public SettingsWindow(MainWindow widget)
 {
  this.widget = widget; Title = "Settings — Prototype"; Width = 440; Height = 720; MinWidth = 400; MinHeight = 400;
  FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
  SetResourceReference(BackgroundProperty, "Surface");
  Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
  Heading("Settings", 25); Heading("Refresh interval");
  var interval = new ComboBox { ItemsSource = new[] { "1 minute", "5 minutes", "10 minutes", "15 minutes", "30 minutes", "60 minutes" }, SelectedIndex = 2, Padding = new Thickness(8) }; panel.Children.Add(interval);
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
  foreach (var size in new[] { ("S", 330d), ("M", 390d), ("L", 450d) })
  {
   var button = new Button { Content = size.Item1, Width = 90 }; button.Click += (_, _) => { widget.Width = size.Item2; widget.LayoutTransform = new ScaleTransform(size.Item2 / 390, size.Item2 / 390); }; sizes.Children.Add(button);
  }
  Heading("Account"); var login = new Button { Content = "Sign in · unavailable in prototype", IsEnabled = false }; panel.Children.Add(login);
  Check("Remember me", true, _ => { });
  var startup = new CheckBox { Content = "Launch at startup · next step", IsEnabled = false }; panel.Children.Add(startup);
  Heading("Theme"); var themes = Row();
  foreach (string theme in new[] { "Light", "Dark", "Adaptive glass" })
  {
   var button = new Button { Content = theme, Padding = new Thickness(10, 12, 10, 12) }; button.Click += (_, _) => { widget.Theme = theme; widget.RefreshView(); }; themes.Children.Add(button);
  }
  Note("Glass preview uses transparency; native blur and automatic adaptation come later.");
  Check("Lock position", widget.Locked, value => { widget.Locked = value; widget.RefreshView(); });
  Note("Settings are temporary in this prototype.");
 }
 void Heading(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 10) });
 void Note(string text) { var note = new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(note); }
 StackPanel Row() { var row = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(row); return row; }
 void Check(string label, bool initial, Action<bool> action) { var box = new CheckBox { Content = label, IsChecked = initial }; box.Checked += (_, _) => action(true); box.Unchecked += (_, _) => action(false); panel.Children.Add(box); }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LimitWidget;

public sealed class ColorPickerWindow : Window
{
    readonly Canvas spectrum = new() { Width = 304, Height = 148, ClipToBounds = true };
    readonly Ellipse marker = new() { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false };
    readonly Slider hue = new() { Minimum = 0, Maximum = 359.99, Margin = new Thickness(0, 5, 0, 6) };
    readonly TextBox hex = new() { Margin = new Thickness(2), Padding = new Thickness(5), MaxLength = 7 };
    readonly TextBox[] rgb = Enumerable.Range(0, 3).Select(_ => new TextBox { Margin = new Thickness(2), Padding = new Thickness(5), MaxLength = 3 }).ToArray();
    readonly Border preview = new() { Height = 30, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 6, 0, 4) };
    readonly TextBlock validation = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    bool updating;
    double saturation, brightness;
    public Color SelectedColor { get; private set; }

    public ColorPickerWindow(string title, Color initial)
    {
        Title = title; Width = 350; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        SetResourceReference(BackgroundProperty, "ButtonSurface"); SetResourceReference(ForegroundProperty, "Foreground");
        var panel = new StackPanel { Margin = new Thickness(14) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        spectrum.Children.Add(new Rectangle { Width = 304, Height = 148, Fill = new LinearGradientBrush(Colors.White, Colors.Transparent, 0), IsHitTestVisible = false });
        spectrum.Children.Add(new Rectangle { Width = 304, Height = 148, Fill = new LinearGradientBrush(Colors.Transparent, Colors.Black, 90), IsHitTestVisible = false });
        spectrum.Children.Add(marker); panel.Children.Add(spectrum);
        spectrum.MouseLeftButtonDown += (_, e) => { spectrum.CaptureMouse(); SelectPoint(e.GetPosition(spectrum)); };
        spectrum.MouseMove += (_, e) => { if (spectrum.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) SelectPoint(e.GetPosition(spectrum)); };
        spectrum.MouseLeftButtonUp += (_, _) => spectrum.ReleaseMouseCapture();
        var rainbow = new LinearGradientBrush();
        foreach (var stop in new[] { (Colors.Red, 0d), (Colors.Yellow, 1d / 6), (Colors.Lime, 2d / 6), (Colors.Cyan, 3d / 6), (Colors.Blue, 4d / 6), (Colors.Magenta, 5d / 6), (Colors.Red, 1d) }) rainbow.GradientStops.Add(new GradientStop(stop.Item1, stop.Item2));
        panel.Children.Add(new Border { Height = 12, Background = rainbow, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 9, 0, 0) });
        panel.Children.Add(hue); hue.ValueChanged += (_, _) => { if (!updating) UpdateFromSpectrum(); };
        var fields = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        for (int i = 0; i < 3; i++)
        {
            var field = new StackPanel(); field.Children.Add(new TextBlock { Text = new[] { "R", "G", "B" }[i] }); field.Children.Add(rgb[i]); fields.Children.Add(field);
            rgb[i].TextChanged += (_, _) =>
            {
                if (updating) return;
                if (rgb.All(box => byte.TryParse(box.Text, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                    SetColor(Color.FromRgb(byte.Parse(rgb[0].Text, CultureInfo.InvariantCulture), byte.Parse(rgb[1].Text, CultureInfo.InvariantCulture), byte.Parse(rgb[2].Text, CultureInfo.InvariantCulture)));
                else validation.Text = "RGB values must be between 0 and 255.";
            };
        }
        panel.Children.Add(fields); panel.Children.Add(new TextBlock { Text = "HEX", Margin = new Thickness(0, 5, 0, 0) }); panel.Children.Add(hex);
        hex.TextChanged += (_, _) =>
        {
            if (updating) return;
            string value = hex.Text.TrimStart('#');
            if (value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed))
                SetColor(Color.FromRgb((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed));
            else validation.Text = "Enter a six-digit HEX color, for example #39DFB5.";
        };
        panel.Children.Add(preview); panel.Children.Add(validation);
        var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var apply = new Button { Content = "Apply", IsDefault = true };
        apply.Click += (_, _) => { if (string.IsNullOrEmpty(validation.Text)) DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(apply); panel.Children.Add(buttons);
        SetColor(initial);
    }

    void SelectPoint(Point point)
    {
        saturation = Math.Clamp(point.X / spectrum.Width, 0, 1);
        brightness = 1 - Math.Clamp(point.Y / spectrum.Height, 0, 1); UpdateFromSpectrum();
    }
    void SetColor(Color color)
    {
        var hsv = ColorMath.ToHsv(color.R, color.G, color.B);
        updating = true; hue.Value = hsv.Hue; updating = false;
        saturation = hsv.Saturation; brightness = hsv.Value; UpdateFromSpectrum();
    }
    void UpdateFromSpectrum()
    {
        var value = ColorMath.FromHsv(hue.Value, saturation, brightness); SelectedColor = Color.FromRgb(value.R, value.G, value.B);
        var pure = ColorMath.FromHsv(hue.Value, 1, 1); spectrum.Background = new SolidColorBrush(Color.FromRgb(pure.R, pure.G, pure.B));
        Canvas.SetLeft(marker, saturation * spectrum.Width - 6); Canvas.SetTop(marker, (1 - brightness) * spectrum.Height - 6);
        preview.Background = new SolidColorBrush(SelectedColor); validation.Text = "";
        updating = true;
        rgb[0].Text = value.R.ToString(); rgb[1].Text = value.G.ToString(); rgb[2].Text = value.B.ToString();
        hex.Text = $"#{value.R:X2}{value.G:X2}{value.B:X2}"; updating = false;
    }
}

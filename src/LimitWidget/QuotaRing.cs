using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace LimitWidget;
public sealed class QuotaRing : FrameworkElement
{
 public double? Percent { get; set; }
 public Brush RingBrush { get; set; } = Brushes.MediumAquamarine;
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);
  var center = new Point(ActualWidth / 2, ActualHeight / 2);
  double radius = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 9);
  dc.DrawEllipse(null, new Pen((Brush)FindResource("Line"), 9), center, radius, radius);
  double p = Math.Clamp(Percent ?? 0, 0, 100);
  var pen = new Pen(RingBrush, 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
  if (p >= 100) dc.DrawEllipse(null, pen, center, radius, radius);
  else if (p > 0)
  {
   double angle = p / 100 * Math.PI * 2;
   var geometry = new StreamGeometry();
   using (var context = geometry.Open())
   {
    context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
    context.ArcTo(new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle)), new Size(radius, radius), 0, p > 50, SweepDirection.Clockwise, true, false);
   }
   dc.DrawGeometry(null, pen, geometry);
  }
  DrawText(dc, Percent.HasValue ? $"{p:0}%" : "—", 27, center.Y - 29, (Brush)FindResource("Foreground"), FontWeights.SemiBold);
  DrawText(dc, Percent.HasValue ? "remaining" : "no data", 11, center.Y + 8, (Brush)FindResource("Muted"), FontWeights.Normal);
 }
 void DrawText(DrawingContext dc, string value, double size, double y, Brush brush, FontWeight weight)
 {
  var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
  dc.DrawText(text, new Point((ActualWidth - text.Width) / 2, y));
 }
}

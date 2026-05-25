using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KaraokeClub.ViewModels;

namespace KaraokeClub.Controls
{
    // ══ Bar Chart ════════════════════════════════════════════════════
    public class BarChart : Canvas
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items),
                typeof(IEnumerable<ChartPoint>), typeof(BarChart),
                new PropertyMetadata(null, OnItemsChanged));

        public IEnumerable<ChartPoint> Items
        {
            get => (IEnumerable<ChartPoint>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((BarChart)d).Redraw();

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            Redraw();
        }

        private void Redraw()
        {
            Children.Clear();
            var data = Items?.ToList();
            if (data == null || data.Count == 0 || ActualWidth < 1 || ActualHeight < 1) return;

            double w = ActualWidth;
            double h = ActualHeight;
            double maxVal = data.Max(p => p.Value);
            if (maxVal == 0) maxVal = 1;

            double padL = 50, padB = 40, padT = 20, padR = 10;
            double chartW = w - padL - padR;
            double chartH = h - padB - padT;

            int n = data.Count;
            double barW = Math.Max(2, (chartW / n) * 0.65);
            double gap = (chartW / n) * 0.35;

            // Y grid lines
            for (int i = 0; i <= 4; i++)
            {
                double yv = maxVal * i / 4;
                double y = padT + chartH - (chartH * i / 4);

                var line = new Line
                {
                    X1 = padL,
                    Y1 = y,
                    X2 = padL + chartW,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                Children.Add(line);

                var label = MakeText($"{yv:F0}", 9, Color.FromRgb(100, 120, 100));
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 7);
                Children.Add(label);
            }

            // Bars
            for (int i = 0; i < n; i++)
            {
                var pt = data[i];
                double barH = Math.Max(2, chartH * pt.Value / maxVal);
                double x = padL + i * (chartW / n) + gap / 2;
                double y = padT + chartH - barH;

                Color color;
                try { color = (Color)ColorConverter.ConvertFromString(pt.Color); }
                catch { color = Color.FromRgb(94, 53, 177); }

                // Shadow
                var shadow = new Rectangle
                {
                    Width = barW + 4,
                    Height = barH,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(shadow, x + 3);
                Canvas.SetTop(shadow, y + 3);
                Children.Add(shadow);

                // Bar
                var rect = new Rectangle
                {
                    Width = barW,
                    Height = barH,
                    Fill = new LinearGradientBrush(
                        Color.FromArgb(255,
                            (byte)Math.Min(255, color.R + 40),
                            (byte)Math.Min(255, color.G + 40),
                            (byte)Math.Min(255, color.B + 40)),
                        color, 90),
                    RadiusX = 4,
                    RadiusY = 4,
                    ToolTip = pt.Tooltip
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                Children.Add(rect);

                // Value label on top
                if (barH > 18)
                {
                    var valLbl = MakeText($"{pt.Value:F0}", 8, Colors.White);
                    Canvas.SetLeft(valLbl, x);
                    Canvas.SetTop(valLbl, y + 3);
                    Children.Add(valLbl);
                }

                // X label
                var xLbl = MakeText(pt.Label, 8, Color.FromRgb(80, 80, 120));
                xLbl.Width = barW + 10;
                xLbl.TextWrapping = TextWrapping.Wrap;
                xLbl.TextAlignment = TextAlignment.Center;
                Canvas.SetLeft(xLbl, x - 5);
                Canvas.SetTop(xLbl, padT + chartH + 4);
                Children.Add(xLbl);
            }
        }

        private TextBlock MakeText(string text, int size, Color color)
            => new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = new SolidColorBrush(color)
            };
    }

    // ══ Pie Chart ════════════════════════════════════════════════════
    public class PieChart : Canvas
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items),
                typeof(IEnumerable<PieSlice>), typeof(PieChart),
                new PropertyMetadata(null, OnItemsChanged));

        public IEnumerable<PieSlice> Items
        {
            get => (IEnumerable<PieSlice>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((PieChart)d).Redraw();

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            Redraw();
        }

        private void Redraw()
        {
            Children.Clear();
            var data = Items?.ToList();
            if (data == null || data.Count == 0 || ActualWidth < 1 || ActualHeight < 1) return;

            double cx = ActualWidth / 2;
            double cy = ActualHeight * 0.45;
            double r = Math.Min(ActualWidth, ActualHeight * 0.8) * 0.38;
            double total = data.Sum(x => x.Value);
            if (total == 0) return;

            double angle = -90;
            foreach (var slice in data)
            {
                double sweep = 360.0 * slice.Value / total;
                Color color;
                try { color = (Color)ColorConverter.ConvertFromString(slice.Color); }
                catch { color = Colors.Gray; }

                var path = MakeSlice(cx, cy, r, angle, sweep, color, slice.Tooltip);
                Children.Add(path);

                // Percentage label
                double midA = (angle + sweep / 2) * Math.PI / 180;
                double lx = cx + Math.Cos(midA) * (r * 0.65);
                double ly = cy + Math.Sin(midA) * (r * 0.65);
                var lbl = new TextBlock
                {
                    Text = $"{slice.Percentage:F1}%",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                lbl.Measure(new Size(100, 100));
                Canvas.SetLeft(lbl, lx - lbl.DesiredSize.Width / 2);
                Canvas.SetTop(lbl, ly - lbl.DesiredSize.Height / 2);
                Children.Add(lbl);

                angle += sweep;
            }

            // Legend
            double legendY = cy + r + 14;
            double spacing = ActualWidth / (data.Count + 1);
            for (int i = 0; i < data.Count; i++)
            {
                Color color;
                try { color = (Color)ColorConverter.ConvertFromString(data[i].Color); }
                catch { color = Colors.Gray; }
                double lx = spacing * (i + 1);

                var dot = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(color)
                };
                Canvas.SetLeft(dot, lx - 30);
                Canvas.SetTop(dot, legendY + 2);
                Children.Add(dot);

                var lbl = new TextBlock
                {
                    Text = data[i].Label,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 120))
                };
                Canvas.SetLeft(lbl, lx - 18);
                Canvas.SetTop(lbl, legendY);
                Children.Add(lbl);
            }
        }

        private Path MakeSlice(double cx, double cy, double r, double startAngle, double sweepAngle,
            Color color, string tooltip)
        {
            if (sweepAngle >= 360) sweepAngle = 359.99;
            double sa = startAngle * Math.PI / 180;
            double ea = (startAngle + sweepAngle) * Math.PI / 180;

            var p1 = new Point(cx + r * Math.Cos(sa), cy + r * Math.Sin(sa));
            var p2 = new Point(cx + r * Math.Cos(ea), cy + r * Math.Sin(ea));

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(cx, cy) };
            fig.Segments.Add(new LineSegment(p1, true));
            fig.Segments.Add(new ArcSegment(p2, new Size(r, r), 0,
                sweepAngle > 180, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(new Point(cx, cy), true));
            fig.IsClosed = true;
            geo.Figures.Add(fig);

            return new Path
            {
                Data = geo,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }
    }

    // ══ Line Chart ═══════════════════════════════════════════════════
    public class LineChart : Canvas
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items),
                typeof(IEnumerable<ChartPoint>), typeof(LineChart),
                new PropertyMetadata(null, OnItemsChanged));

        public IEnumerable<ChartPoint> Items
        {
            get => (IEnumerable<ChartPoint>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((LineChart)d).Redraw();

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            Redraw();
        }

        private void Redraw()
        {
            Children.Clear();
            var data = Items?.ToList();
            if (data == null || data.Count == 0 || ActualWidth < 1 || ActualHeight < 1) return;

            double w = ActualWidth;
            double h = ActualHeight;
            double maxVal = data.Max(p => p.Value);
            if (maxVal == 0) maxVal = 1;

            double padL = 50, padB = 35, padT = 20, padR = 15;
            double chartW = w - padL - padR;
            double chartH = h - padB - padT;

            int n = data.Count;

            // Grid lines
            for (int i = 0; i <= 4; i++)
            {
                double yv = maxVal * i / 4;
                double y = padT + chartH - (chartH * i / 4);

                var line = new Line
                {
                    X1 = padL,
                    Y1 = y,
                    X2 = padL + chartW,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                Children.Add(line);

                var label = new TextBlock
                {
                    Text = $"{yv:F0}",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 100))
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 7);
                Children.Add(label);
            }

            // Compute points
            var pts = data.Select((p, i) => new Point(
                padL + i * (n > 1 ? chartW / (n - 1) : 0),
                padT + chartH - chartH * p.Value / maxVal
            )).ToList();

            if (pts.Count < 2) return;

            // Fill area under line
            var areaFig = new PathFigure { StartPoint = new Point(pts[0].X, padT + chartH) };
            areaFig.Segments.Add(new LineSegment(pts[0], true));
            for (int i = 1; i < pts.Count; i++)
            {
                var cp1 = new Point((pts[i - 1].X + pts[i].X) / 2, pts[i - 1].Y);
                var cp2 = new Point((pts[i - 1].X + pts[i].X) / 2, pts[i].Y);
                areaFig.Segments.Add(new BezierSegment(cp1, cp2, pts[i], true));
            }
            areaFig.Segments.Add(new LineSegment(new Point(pts.Last().X, padT + chartH), true));
            areaFig.IsClosed = true;

            var areaGeo = new PathGeometry();
            areaGeo.Figures.Add(areaFig);
            Children.Add(new Path
            {
                Data = areaGeo,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(80, 94, 53, 177),
                    Color.FromArgb(0, 94, 53, 177),
                    90)
            });

            // Line
            var lineFig = new PathFigure { StartPoint = pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                var cp1 = new Point((pts[i - 1].X + pts[i].X) / 2, pts[i - 1].Y);
                var cp2 = new Point((pts[i - 1].X + pts[i].X) / 2, pts[i].Y);
                lineFig.Segments.Add(new BezierSegment(cp1, cp2, pts[i], true));
            }
            var lineGeo = new PathGeometry();
            lineGeo.Figures.Add(lineFig);
            Children.Add(new Path
            {
                Data = lineGeo,
                Stroke = new SolidColorBrush(Color.FromRgb(94, 53, 177)),
                StrokeThickness = 2.5,
                Fill = null
            });

            // Dots + labels
            for (int i = 0; i < n; i++)
            {
                var pt = data[i];
                var pos = pts[i];

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Colors.White),
                    Stroke = new SolidColorBrush(Color.FromRgb(94, 53, 177)),
                    StrokeThickness = 2,
                    ToolTip = pt.Tooltip
                };
                Canvas.SetLeft(dot, pos.X - 4);
                Canvas.SetTop(dot, pos.Y - 4);
                Children.Add(dot);

                var xLbl = new TextBlock
                {
                    Text = pt.Label,
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 120)),
                    TextAlignment = TextAlignment.Center,
                    Width = 50
                };
                Canvas.SetLeft(xLbl, pos.X - 25);
                Canvas.SetTop(xLbl, padT + chartH + 4);
                Children.Add(xLbl);
            }
        }
    }
}

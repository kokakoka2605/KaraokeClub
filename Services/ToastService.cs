using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KaraokeClub.Services
{
    public enum ToastKind { Success, Error, Warning }

    public static class ToastService
    {
        private static Panel? _host;

        public static void Register(Panel host) => _host = host;

        public static void Show(string message, ToastKind kind = ToastKind.Success, int durationMs = 2800)
        {
            if (_host == null) return;

            _host.Dispatcher.Invoke(() =>
            {
                var (bg, icon) = kind switch
                {
                    ToastKind.Success => ("#4a7c1f", "✔"),
                    ToastKind.Error => ("#b03030", "✖"),
                    ToastKind.Warning => ("#b07830", "⚠"),
                    _ => ("#4a7c1f", "✔")
                };

                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 10, 16, 10),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Opacity = 0,
                    MinWidth = 240,
                    MaxWidth = 420,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 14,
                        ShadowDepth = 2,
                        Opacity = 0.5,
                        Color = Colors.Black
                    }
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = icon + "  ",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                });
                border.Child = sp;
                _host.Children.Add(border);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
                border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));
                    fadeOut.Completed += (_, _) => _host.Children.Remove(border);
                    border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                timer.Start();
            });
        }
    }
}

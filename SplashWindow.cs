using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;

namespace NaxiBootstrap;

internal class SplashWindow : Window
{
    private readonly TextBlock _status;

    public SplashWindow()
    {
        var cfg = RobloxLauncher.LoadConfig();
        var cfgFont = cfg.FontName;
        if (!string.IsNullOrWhiteSpace(cfgFont)) FontFamily = new FontFamily(cfgFont);
        var isMax = cfg.IsMax; var pro = cfg.IsPro || isMax;
        Width = 480;
        Height = 150;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var logo = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(9),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isMax)
        {
            logo.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(139, 92, 246), 0),
                    new GradientStop(Color.FromRgb(91, 33, 182), 1)
                }
            };
        }
        else if (pro)
        {
            logo.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(231, 200, 119), 0),
                    new GradientStop(Color.FromRgb(192, 150, 63), 1)
                }
            };
        }
        else
        {
            logo.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(143, 208, 240), 0),
                    new GradientStop(Color.FromRgb(74, 140, 178), 1)
                }
            };
        }
        logo.Child = new TextBlock
        {
            Text = "N",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(13, 20, 24)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(logo);
        head.Children.Add(new TextBlock
        {
            Text = isMax ? "Launching NaxiBootstrap MAX" : pro ? "Launching Naxi Bootstrap PRO" : "Naxi Bootstrap",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 242, 242)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        });

        _status = new TextBlock
        {
            Text = "Запускаем Roblox...",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 139, 146)),
            Margin = new Thickness(0, 18, 0, 0)
        };

        var track = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Margin = new Thickness(0, 20, 0, 0),
            ClipToBounds = true
        };

        var pill = new Border
        {
            Width = 110,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        pill.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 130, 187, 216), 0),
                new GradientStop(Color.FromRgb(130, 187, 216), 0.5),
                new GradientStop(Color.FromArgb(0, 130, 187, 216), 1)
            }
        };
        var tt = new TranslateTransform(-120, 0);
        pill.RenderTransform = tt;
        track.Child = pill;

        var stack = new StackPanel { Margin = new Thickness(28) };
        stack.Children.Add(head);
        stack.Children.Add(_status);
        stack.Children.Add(track);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(21, 21, 21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = stack
        };

        Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(-120, 560, TimeSpan.FromMilliseconds(1400))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            tt.BeginAnimation(TranslateTransform.XProperty, anim);
        };
    }

    public void SetStatus(string text) => _status.Text = text;
}

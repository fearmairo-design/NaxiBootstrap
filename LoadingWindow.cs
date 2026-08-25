using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NaxiBootstrap;

internal class LoadingWindow : Window
{
    private const double TrackWidth = 340;
    private readonly TextBlock _status;
    private readonly TextBlock _percent;
    private readonly Border _fill;
    private double _last;

    public LoadingWindow()
    {
        var cfgFont = RobloxLauncher.LoadConfig().FontName;
        if (!string.IsNullOrWhiteSpace(cfgFont)) FontFamily = new FontFamily(cfgFont);
        Width = 430;
        Height = 230;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var logoScale = new ScaleTransform();
        var logo = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = logoScale
        };
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
        logo.Child = new TextBlock
        {
            Text = "N",
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(13, 20, 24)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = new TextBlock
        {
            Text = "Naxi Bootstrap",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 242, 242)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };

        _status = new TextBlock
        {
            Text = "Инициализация...",
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 139, 146)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _fill = new Border
        {
            Width = 0,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _fill.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(96, 165, 205), 0),
                new GradientStop(Color.FromRgb(143, 208, 240), 0.5),
                new GradientStop(Color.FromRgb(167, 139, 250), 1)
            }
        };

        _percent = new TextBlock
        {
            Text = "0%",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(130, 187, 216)),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 46,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(14, 0, 0, 0)
        };

        var barRow = new Grid { Margin = new Thickness(0, 24, 0, 0) };
        barRow.ColumnDefinitions.Add(new ColumnDefinition());
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var track = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            VerticalAlignment = VerticalAlignment.Center
        };
        track.Child = _fill;
        Grid.SetColumn(track, 0);
        barRow.Children.Add(track);
        Grid.SetColumn(_percent, 1);
        barRow.Children.Add(_percent);

        var stack = new StackPanel { Margin = new Thickness(45, 34, 45, 30) };
        stack.Children.Add(logo);
        stack.Children.Add(title);
        stack.Children.Add(_status);
        stack.Children.Add(barRow);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(21, 21, 21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = stack
        };

        Loaded += (_, _) =>
        {
            var pulse = new DoubleAnimation(1, 1.07, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        };
    }

    public void SetProgress(double pct, string? status)
    {
        if (status != null) _status.Text = status;
        _percent.Text = $"{(int)Math.Clamp(pct, 0, 100)}%";
        var target = TrackWidth * Math.Clamp(pct, 0, 100) / 100.0;
        var anim = new DoubleAnimation(_last, target, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _last = target;
        _fill.BeginAnimation(WidthProperty, anim);
    }

    public void FadeOut()
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Application = System.Windows.Application;

namespace NaxiBootstrap;

// Runtime theme engine: overwrites the theme-aware resources defined in App.xaml
// (brushes + colors) so every DynamicResource reference updates live, then pages
// are rebuilt to re-resolve their FindResource-based colors.
internal static class ThemeService
{
    public sealed record AccentColors(string Name, Color Base, Color Light, Color Dark);

    public static readonly AccentColors[] Accents =
    {
        new("Blue",   C("#7CB7E0"), C("#8FC4EA"), C("#5E9CC8")),
        new("Violet", C("#A78BFA"), C("#B79CFF"), C("#8B6CF0")),
        new("Green",  C("#5AD08B"), C("#6FDC9B"), C("#3FA96C")),
        new("Teal",   C("#5EC8C0"), C("#78DCD5"), C("#3FA6A0")),
        new("Orange", C("#F0A35E"), C("#FFB877"), C("#DB8A3F")),
        new("Red",    C("#E86A6A"), C("#F98A8A"), C("#D14E4E")),
        new("Pink",   C("#F078B4"), C("#FF95C8"), C("#DD5B97")),
        new("Gold",   C("#E8C860"), C("#F5D97D"), C("#C9A63F")),
        new("Nebula",  C("#8B7CF6"), C("#9F92FF"), C("#6E5BD8")),
        new("Magenta", C("#E34FD0"), C("#EF6FE0"), C("#BE38AC")),
        new("Toxic",   C("#B4F04A"), C("#C6FF66"), C("#8FCC2E")),
    };

    // accents reserved for Naxi MAX subscribers
    public static readonly HashSet<string> MaxOnly = new() { "Nebula", "Magenta", "Toxic" };

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static void Apply(string theme, string accent)
    {
        bool light = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        var acc = Accents.FirstOrDefault(x => x.Name.Equals(accent, StringComparison.OrdinalIgnoreCase)) ?? Accents[0];
        var res = Application.Current.Resources;

        Solid(res, "Accent", acc.Base);
        Solid(res, "AccentHover", acc.Light);
        Solid(res, "AccentPressed", acc.Dark);
        Solid(res, "AccentText", C("#0A1420"));
        Solid(res, "AccentA", acc.Light);
        Solid(res, "AccentB", acc.Dark);
        res["AccentColorC"] = acc.Base;
        res["AccentLightC"] = acc.Light;
        res["AccentDarkC"] = acc.Dark;

        if (light)
        {
            Solid(res, "Bg", C("#F3F5F9"));
            Solid(res, "Surface", Colors.White);
            Solid(res, "Surface2", Colors.White);
            Solid(res, "Stroke", C("#E2E7EF"));
            Solid(res, "Text", C("#121821"));
            Solid(res, "Muted", C("#5A6572"));
            Solid(res, "CardBorder", C("#DAE1EA"));
            Solid(res, "NavHover", C("#EFF2F7"));
            Solid(res, "InputBg", Colors.White);
            Solid(res, "InputBorder", C("#CCD5E0"));
            Solid(res, "ToggleTrack", C("#C6D0DC"));
            Solid(res, "KnobColor", Colors.White);
            Solid(res, "NavShellBg", Colors.White);
            Solid(res, "NavShellBorder", C("#DAE1EA"));
            Solid(res, "LogHeader", C("#5A6572"));
            Solid(res, "PillBg", Colors.White);
            Solid(res, "PillBorder", C("#CFD8E4"));
            Solid(res, "PillBorderHover", C("#AEBAC9"));
            res["TitleGradient"] = VGrad(C("#39414E"), C("#8A94A2"));
        }
        else
        {
            Solid(res, "Bg", C("#0B0D12"));
            Solid(res, "Surface", C("#14171E"));
            Solid(res, "Surface2", C("#191D25"));
            Solid(res, "Stroke", C("#232833"));
            Solid(res, "Text", C("#EEF1F5"));
            Solid(res, "Muted", C("#8A9099"));
            Solid(res, "CardBorder", C("#20252E"));
            Solid(res, "NavHover", C("#1A1F28"));
            Solid(res, "InputBg", C("#10131A"));
            Solid(res, "InputBorder", C("#262B34"));
            Solid(res, "ToggleTrack", C("#262B34"));
            Solid(res, "KnobColor", C("#D6DAE0"));
            Solid(res, "NavShellBg", C("#12151B"));
            Solid(res, "NavShellBorder", C("#20252E"));
            Solid(res, "LogHeader", C("#8A9099"));
            Solid(res, "PillBg", C("#171B22"));
            Solid(res, "PillBorder", C("#2A303A"));
            Solid(res, "PillBorderHover", C("#39414E"));
            res["TitleGradient"] = VGrad(Colors.White, C("#AEB6C2"));
        }
    }

    private static void Solid(ResourceDictionary res, string key, Color c) => res[key] = new SolidColorBrush(c);

    private static LinearGradientBrush VGrad(Color top, Color bottom) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) }
    };
}

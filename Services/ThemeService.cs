using System.Windows;
using System.Windows.Media;

namespace HerculesAudioControl.Services;

public static class ThemeService
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string Neon = "ZLD Neon";

    public static string CurrentTheme { get; private set; } = Neon;

    public static void Apply(string? theme)
    {
        CurrentTheme = Normalize(theme);

        ThemePalette palette = CurrentTheme switch
        {
            // "Light" is intentionally a dim lavender theme.
            // It avoids the former large white surface.
            Light => new ThemePalette(
                Background: "#151027",
                Background2: "#21163A",
                Panel: "#21163D",
                Panel2: "#2A1B4C",
                PanelHover: "#34225D",
                Sidebar: "#100B22",
                Secondary: "#30204F",
                Border: "#7652B8",
                Accent: "#E84DAE",
                Accent2: "#65D8FF",
                Accent3: "#9A78FF",
                Text: "#F1EAFE",
                MutedText: "#C0AFE8",
                ButtonText: "#FFF5FC",
                Success: "#23E6B1"),

            Neon => new ThemePalette(
                Background: "#030617",
                Background2: "#10062B",
                Panel: "#090B25",
                Panel2: "#160A35",
                PanelHover: "#1E1048",
                Sidebar: "#050719",
                Secondary: "#171735",
                Border: "#5940A5",
                Accent: "#FF0A91",
                Accent2: "#00D9FF",
                Accent3: "#9D4DFF",
                Text: "#F3F0FF",
                MutedText: "#B69BFF",
                ButtonText: "#FFFFFF",
                Success: "#18E4A5"),

            _ => new ThemePalette(
                Background: "#070A18",
                Background2: "#10172C",
                Panel: "#11162B",
                Panel2: "#161D36",
                PanelHover: "#202A49",
                Sidebar: "#090D1D",
                Secondary: "#202944",
                Border: "#34446D",
                Accent: "#865DFF",
                Accent2: "#00C7E8",
                Accent3: "#C24CFF",
                Text: "#EEF3FF",
                MutedText: "#9EACD1",
                ButtonText: "#FFFFFF",
                Success: "#20DDA6")
        };

        ApplyPalette(palette);
    }

    public static string Next(string? current)
    {
        return Normalize(current) switch
        {
            Dark => Light,
            Light => Neon,
            _ => Dark
        };
    }

    public static string ButtonLabel(string? current)
    {
        return Normalize(current) switch
        {
            Dark => "◐ Soft",
            Light => "✦ ZLD Neon",
            _ => "☾ Dark"
        };
    }

    private static string Normalize(string? theme)
    {
        if (string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase))
            return Light;

        if (string.Equals(theme, Neon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(theme, "Neon", StringComparison.OrdinalIgnoreCase))
            return Neon;

        return Dark;
    }

    private static void ApplyPalette(ThemePalette palette)
    {
        SetColor("BackgroundColor", palette.Background);
        SetColor("Background2Color", palette.Background2);
        SetColor("PanelColor", palette.Panel);
        SetColor("Panel2Color", palette.Panel2);
        SetColor("PanelHoverColor", palette.PanelHover);
        SetColor("SidebarColor", palette.Sidebar);
        SetColor("SecondaryColor", palette.Secondary);
        SetColor("BorderColor", palette.Border);
        SetColor("AccentColor", palette.Accent);
        SetColor("Accent2Color", palette.Accent2);
        SetColor("Accent3Color", palette.Accent3);
        SetColor("TextColor", palette.Text);
        SetColor("MutedTextColor", palette.MutedText);
        SetColor("ButtonTextColor", palette.ButtonText);
        SetColor("SuccessColor", palette.Success);

        SetBrush("BackgroundBrush", "BackgroundColor");
        SetBrush("Background2Brush", "Background2Color");
        SetBrush("PanelBrush", "PanelColor");
        SetBrush("Panel2Brush", "Panel2Color");
        SetBrush("PanelHoverBrush", "PanelHoverColor");
        SetBrush("SidebarBrush", "SidebarColor");
        SetBrush("SecondaryBrush", "SecondaryColor");
        SetBrush("BorderBrush", "BorderColor");
        SetBrush("AccentBrush", "AccentColor");
        SetBrush("Accent2Brush", "Accent2Color");
        SetBrush("Accent3Brush", "Accent3Color");
        SetBrush("TextBrush", "TextColor");
        SetBrush("MutedTextBrush", "MutedTextColor");
        SetBrush("ButtonTextBrush", "ButtonTextColor");
        SetBrush("SuccessBrush", "SuccessColor");

        Application.Current.Resources["AppBackgroundBrush"] =
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new((Color)Application.Current.Resources["BackgroundColor"], 0),
                    new((Color)Application.Current.Resources["Background2Color"], 1)
                },
                new Point(0, 0),
                new Point(1, 1));

        Application.Current.Resources["AccentGradientBrush"] =
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new((Color)Application.Current.Resources["Accent2Color"], 0),
                    new((Color)Application.Current.Resources["Accent3Color"], 0.48),
                    new((Color)Application.Current.Resources["AccentColor"], 1)
                },
                new Point(0, 0.5),
                new Point(1, 0.5));
    }

    private static void SetColor(string key, string value) =>
        Application.Current.Resources[key] =
            (Color)ColorConverter.ConvertFromString(value);

    private static void SetBrush(string brushKey, string colorKey)
    {
        var color = (Color)Application.Current.Resources[colorKey];
        Application.Current.Resources[brushKey] = new SolidColorBrush(color);
    }

    private sealed record ThemePalette(
        string Background,
        string Background2,
        string Panel,
        string Panel2,
        string PanelHover,
        string Sidebar,
        string Secondary,
        string Border,
        string Accent,
        string Accent2,
        string Accent3,
        string Text,
        string MutedText,
        string ButtonText,
        string Success);
}

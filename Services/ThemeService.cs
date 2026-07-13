using System.Windows;
using System.Windows.Media;

namespace HerculesAudioControl.Services;

public static class ThemeService
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string Neon = "ZLD Neon";

    public static string CurrentTheme { get; private set; } = Dark;

    public static void Apply(string? theme)
    {
        CurrentTheme = Normalize(theme);

        ThemePalette palette = CurrentTheme switch
        {
            Light => new ThemePalette(
                Background: "#EEF1F8",
                Panel: "#F8F9FD",
                PanelHover: "#E2E7F2",
                Sidebar: "#E7EBF5",
                Secondary: "#D9DFEC",
                Accent: "#7257E8",
                Accent2: "#1976D2",
                Text: "#192238",
                MutedText: "#5F6B84",
                ButtonText: "#F4F6FF"),

            Neon => new ThemePalette(
                Background: "#050316",
                Panel: "#10082B",
                PanelHover: "#1B0D43",
                Sidebar: "#09051E",
                Secondary: "#241052",
                Accent: "#FF1493",
                Accent2: "#00C8FF",
                Text: "#BFF7FF",
                MutedText: "#B29DFF",
                ButtonText: "#09051E"),

            _ => new ThemePalette(
                Background: "#0B1020",
                Panel: "#141B2D",
                PanelHover: "#1A2440",
                Sidebar: "#0E1528",
                Secondary: "#28324B",
                Accent: "#7C5CFF",
                Accent2: "#00D4AA",
                Text: "#F5F7FF",
                MutedText: "#9BA6C1",
                ButtonText: "#F5F7FF")
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
            Dark => "☀ Light",
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
        SetColor("PanelColor", palette.Panel);
        SetColor("PanelHoverColor", palette.PanelHover);
        SetColor("SidebarColor", palette.Sidebar);
        SetColor("SecondaryColor", palette.Secondary);
        SetColor("AccentColor", palette.Accent);
        SetColor("Accent2Color", palette.Accent2);
        SetColor("TextColor", palette.Text);
        SetColor("MutedTextColor", palette.MutedText);
        SetColor("ButtonTextColor", palette.ButtonText);

        SetBrush("BackgroundBrush", "BackgroundColor");
        SetBrush("PanelBrush", "PanelColor");
        SetBrush("PanelHoverBrush", "PanelHoverColor");
        SetBrush("SidebarBrush", "SidebarColor");
        SetBrush("SecondaryBrush", "SecondaryColor");
        SetBrush("AccentBrush", "AccentColor");
        SetBrush("Accent2Brush", "Accent2Color");
        SetBrush("TextBrush", "TextColor");
        SetBrush("MutedTextBrush", "MutedTextColor");
        SetBrush("ButtonTextBrush", "ButtonTextColor");
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
        string Panel,
        string PanelHover,
        string Sidebar,
        string Secondary,
        string Accent,
        string Accent2,
        string Text,
        string MutedText,
        string ButtonText);
}

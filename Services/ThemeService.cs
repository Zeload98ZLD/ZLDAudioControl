using System.Windows;
using System.Windows.Media;

namespace HerculesAudioControl.Services;

public static class ThemeService
{
    public static string CurrentTheme { get; private set; } = "Dark";

    public static void Apply(string theme)
    {
        bool light = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        CurrentTheme = light ? "Light" : "Dark";

        SetColor("BackgroundColor", light ? "#F2F5FA" : "#0B1020");
        SetColor("PanelColor", light ? "#FFFFFF" : "#141B2D");
        SetColor("PanelHoverColor", light ? "#E9EDF5" : "#1A2440");
        SetColor("TextColor", light ? "#172033" : "#F5F7FF");
        SetColor("MutedTextColor", light ? "#657089" : "#9BA6C1");

        SetBrush("BackgroundBrush", "BackgroundColor");
        SetBrush("PanelBrush", "PanelColor");
        SetBrush("PanelHoverBrush", "PanelHoverColor");
        SetBrush("TextBrush", "TextColor");
        SetBrush("MutedTextBrush", "MutedTextColor");
    }

    private static void SetColor(string key, string value) =>
        Application.Current.Resources[key] =
            (Color)ColorConverter.ConvertFromString(value);

    private static void SetBrush(string brushKey, string colorKey)
    {
        var color = (Color)Application.Current.Resources[colorKey];
        Application.Current.Resources[brushKey] = new SolidColorBrush(color);
    }
}

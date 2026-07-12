using System.IO;
using System.Text.Json;
using HerculesAudioControl.Models;

namespace HerculesAudioControl.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);

        string directory = Path.Combine(appData, "ZLDAudioControl");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");

        // Einstellungen älterer HAC-Versionen einmalig übernehmen.
        string oldSettings = Path.Combine(
            appData,
            "HerculesAudioControl",
            "settings.json");

        if (!File.Exists(_settingsPath) && File.Exists(oldSettings))
        {
            try
            {
                File.Copy(oldSettings, _settingsPath);
            }
            catch
            {
                // Bei einer fehlgeschlagenen Migration mit Standardwerten starten.
            }
        }
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}

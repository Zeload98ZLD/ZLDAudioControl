using System.Globalization;
using System.IO;
using HerculesAudioControl.Models;

namespace HerculesAudioControl.Services;

public sealed class EqualizerService
{
    public string BuildConfiguration(EqualizerSettings settings)
    {
        string number(double value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        var lines = new List<string>
        {
            "# ZLD Audio Control - automatisch erzeugt",
            $"Filter: ON PK Fc 95 Hz Gain {number(settings.BassGain)} dB Q 0.70",
            $"Filter: ON PK Fc 1000 Hz Gain {number(settings.MidGain)} dB Q 0.85",
            $"Filter: ON PK Fc 8500 Hz Gain {number(settings.TrebleGain)} dB Q 0.70"
        };

        string filter = BuildFilter(settings.FilterPosition);
        if (!string.IsNullOrWhiteSpace(filter))
            lines.Add(filter);

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public string BuildFilter(double position)
    {
        position = Math.Clamp(position, 0, 100);

        if (position is >= 47 and <= 53)
            return "";

        if (position < 47)
        {
            double ratio = position / 47.0;
            double frequency = 250.0 * Math.Pow(18000.0 / 250.0, ratio);
            return $"Filter: ON LP Fc {frequency.ToString("0.0", CultureInfo.InvariantCulture)} Hz Q 0.707";
        }

        double hpRatio = (position - 53.0) / 47.0;
        double hpFrequency = 30.0 * Math.Pow(8000.0 / 30.0, hpRatio);
        return $"Filter: ON HP Fc {hpFrequency.ToString("0.0", CultureInfo.InvariantCulture)} Hz Q 0.707";
    }

    public void Apply(EqualizerSettings settings)
    {
        if (!settings.Enabled)
            return;

        string? directory = Path.GetDirectoryName(settings.OutputFile);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            settings.OutputFile,
            BuildConfiguration(settings));
    }

    public bool IsEqualizerApoLikelyInstalled() =>
        Directory.Exists(@"C:\Program Files\EqualizerAPO");
}

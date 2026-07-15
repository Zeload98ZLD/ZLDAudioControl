using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HerculesAudioControl.Models;
using NAudio.CoreAudioApi;

namespace HerculesAudioControl.Services;

public sealed record AudioSourceInfo(
    int ProcessId,
    string ProcessName,
    string DisplayName,
    float Volume,
    bool Muted,
    float Peak,
    int SessionCount)
{
    public int VolumePercent => (int)Math.Round(Volume * 100);
    public int PeakPercent => (int)Math.Round(Peak * 100);
    public string StatusText => Muted ? "Stumm" : Peak > 0.001f ? "Gibt Ton aus" : "Audio bereit";
    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? "?"
            : DisplayName[..1].ToUpperInvariant();
}

public sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;

    private static readonly HashSet<string> GameDetectionIgnoredProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer",
            "SearchHost",
            "StartMenuExperienceHost",
            "ShellExperienceHost",
            "ApplicationFrameHost",
            "SystemSettings",
            "TextInputHost",
            "audiodg",
            "ZLDAudioControl",
            "devenv",
            "WindowsTerminal",
            "powershell",
            "pwsh",
            "cmd",
            "Spotify",
            "Discord",
            "chrome",
            "msedge",
            "firefox"
        };

    public string OutputDeviceName => _device?.FriendlyName ?? "Kein Ausgabegerät";

    public AudioService() => RefreshDevice();

    public void RefreshDevice()
    {
        _device?.Dispose();
        _device = _enumerator.GetDefaultAudioEndpoint(
            DataFlow.Render,
            Role.Multimedia);
    }

    public float GetMasterVolume() =>
        _device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;

    public bool GetMasterMute() =>
        _device?.AudioEndpointVolume.Mute ?? false;

    public void SetMasterVolume(float value)
    {
        if (_device is null) return;
        _device.AudioEndpointVolume.MasterVolumeLevelScalar =
            Math.Clamp(value, 0f, 1f);
    }

    public void SetMasterMute(bool muted)
    {
        if (_device is null) return;
        _device.AudioEndpointVolume.Mute = muted;
    }

    public IReadOnlyList<AudioSourceInfo> GetAudioSources(
        IEnumerable<string>? excludedProcessNames = null)
    {
        if (_device is null)
            return [];

        var excluded = (excludedProcessNames ?? [])
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sources = new Dictionary<string, MutableAudioSource>(
            StringComparer.OrdinalIgnoreCase);

        SessionCollection sessions = _device.AudioSessionManager.Sessions;

        for (int index = 0; index < sessions.Count; index++)
        {
            AudioSessionControl session = sessions[index];

            try
            {
                uint processId = session.GetProcessID;
                if (processId == 0)
                    continue;

                using Process process =
                    Process.GetProcessById((int)processId);

                string processName = process.ProcessName;

                if (excluded.Contains(processName))
                    continue;

                float volume = session.SimpleAudioVolume.Volume;
                bool muted = session.SimpleAudioVolume.Mute;
                float peak = GetPeak(session);

                string displayName = GetSessionDisplayName(
                    session,
                    process,
                    processName);

                if (!sources.TryGetValue(
                        processName,
                        out MutableAudioSource? source))
                {
                    source = new MutableAudioSource
                    {
                        ProcessId = process.Id,
                        ProcessName = processName,
                        DisplayName = displayName,
                        Volume = volume,
                        Muted = muted,
                        Peak = peak,
                        SessionCount = 1
                    };

                    sources[processName] = source;
                    continue;
                }

                source.SessionCount++;
                source.Peak = Math.Max(source.Peak, peak);
                source.Volume = Math.Max(source.Volume, volume);
                source.Muted &= muted;

                if (source.DisplayName.Equals(
                        source.ProcessName,
                        StringComparison.OrdinalIgnoreCase) &&
                    !displayName.Equals(
                        processName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    source.DisplayName = displayName;
                }
            }
            catch
            {
                // Beendete oder geschützte Audiositzungen überspringen.
            }
        }

        return sources.Values
            .Select(source => new AudioSourceInfo(
                source.ProcessId,
                source.ProcessName,
                source.DisplayName,
                source.Volume,
                source.Muted,
                source.Peak,
                source.SessionCount))
            .OrderByDescending(source => source.Peak > 0.001f)
            .ThenByDescending(source => source.Peak)
            .ThenBy(source => source.DisplayName)
            .ToList();
    }

    public bool TrySetProcessVolume(string processName, float volume)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);
        bool found = false;

        foreach (SessionSnapshot item in GetSessionSnapshots())
        {
            try
            {
                if (!item.ProcessName.Equals(
                        wanted,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                item.Session.SimpleAudioVolume.Volume =
                    Math.Clamp(volume, 0f, 1f);

                found = true;
            }
            catch
            {
            }
        }

        return found;
    }

    public bool TrySetProcessMute(string processName, bool muted)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);
        bool found = false;

        foreach (SessionSnapshot item in GetSessionSnapshots())
        {
            try
            {
                if (!item.ProcessName.Equals(
                        wanted,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                item.Session.SimpleAudioVolume.Mute = muted;
                found = true;
            }
            catch
            {
            }
        }

        return found;
    }

    public (float Volume, bool Muted)? TryGetProcessState(
        string processName)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);
        var matches = GetSessionSnapshots()
            .Where(item => item.ProcessName.Equals(
                wanted,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return null;

        float volume = matches.Max(item =>
        {
            try { return item.Session.SimpleAudioVolume.Volume; }
            catch { return 0f; }
        });

        bool muted = matches.All(item =>
        {
            try { return item.Session.SimpleAudioVolume.Mute; }
            catch { return false; }
        });

        return (volume, muted);
    }

    public string? GetForegroundProcessName()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
            return null;

        try
        {
            using Process process =
                Process.GetProcessById((int)processId);

            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    public string? FindBestGameAudioProcess()
    {
        IReadOnlyList<AudioSourceInfo> sources = GetAudioSources();

        string? foreground = GetForegroundProcessName();

        if (!string.IsNullOrWhiteSpace(foreground))
        {
            AudioSourceInfo? foregroundSource = sources.FirstOrDefault(
                source => source.ProcessName.Equals(
                    foreground,
                    StringComparison.OrdinalIgnoreCase));

            if (foregroundSource is not null)
                return foregroundSource.ProcessName;
        }

        return sources
            .Where(source =>
                !GameDetectionIgnoredProcesses.Contains(source.ProcessName))
            .OrderByDescending(source => source.Peak)
            .ThenByDescending(source => source.SessionCount)
            .Select(source => source.ProcessName)
            .FirstOrDefault();
    }

    public void RefreshChannel(AppChannel channel)
    {
        string? processName = channel.Kind == ChannelKind.AutoGame
            ? FindBestGameAudioProcess()
            : channel.ProcessName;

        if (channel.Kind == ChannelKind.AutoGame)
            channel.ActiveTarget = processName ?? "";

        if (string.IsNullOrWhiteSpace(processName))
        {
            channel.IsAvailable = false;
            channel.StatusText = "Keine passende Audioquelle erkannt";
            return;
        }

        var state = TryGetProcessState(processName);
        channel.IsAvailable = state.HasValue;
        channel.StatusText = state.HasValue
            ? "Aktiv"
            : "Wartet auf Audio";

        if (state.HasValue)
        {
            channel.Volume = state.Value.Volume;
            channel.IsMuted = state.Value.Muted;
        }
    }

    public bool SetChannelVolume(AppChannel channel, float volume)
    {
        string? processName = channel.Kind == ChannelKind.AutoGame
            ? channel.ActiveTarget
            : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(processName) &&
               TrySetProcessVolume(processName, volume);
    }

    public bool SetChannelMute(AppChannel channel, bool muted)
    {
        string? processName = channel.Kind == ChannelKind.AutoGame
            ? channel.ActiveTarget
            : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(processName) &&
               TrySetProcessMute(processName, muted);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }

    private IReadOnlyList<SessionSnapshot> GetSessionSnapshots()
    {
        if (_device is null)
            return [];

        var result = new List<SessionSnapshot>();
        SessionCollection sessions = _device.AudioSessionManager.Sessions;

        for (int index = 0; index < sessions.Count; index++)
        {
            AudioSessionControl session = sessions[index];

            try
            {
                uint processId = session.GetProcessID;
                if (processId == 0)
                    continue;

                using Process process =
                    Process.GetProcessById((int)processId);

                result.Add(new SessionSnapshot(
                    session,
                    process.ProcessName));
            }
            catch
            {
            }
        }

        return result;
    }

    private static float GetPeak(AudioSessionControl session)
    {
        try
        {
            return session.AudioMeterInformation.MasterPeakValue;
        }
        catch
        {
            return 0f;
        }
    }

    private static string GetSessionDisplayName(
        AudioSessionControl session,
        Process process,
        string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(session.DisplayName) &&
                !session.DisplayName.StartsWith("@", StringComparison.Ordinal))
            {
                return session.DisplayName;
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
                return process.MainWindowTitle;
        }
        catch
        {
        }

        return FriendlyProcessName(fallback);
    }

    private static string FriendlyProcessName(string processName)
    {
        return processName.ToLowerInvariant() switch
        {
            "spotify" => "Spotify",
            "discord" => "Discord",
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Mozilla Firefox",
            "obs64" => "OBS Studio",
            "vlc" => "VLC media player",
            "ms-teams" => "Microsoft Teams",
            _ => processName
        };
    }

    private sealed class MutableAudioSource
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public float Volume { get; set; }
        public bool Muted { get; set; }
        public float Peak { get; set; }
        public int SessionCount { get; set; }
    }

    private sealed record SessionSnapshot(
        AudioSessionControl Session,
        string ProcessName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);
}

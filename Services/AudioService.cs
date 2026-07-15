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
    string DeviceName,
    float Volume,
    bool Muted,
    float Peak,
    int SessionCount)
{
    public int VolumePercent => (int)Math.Round(Volume * 100);
    public int PeakPercent => (int)Math.Round(Peak * 100);

    public string StatusText =>
        Muted
            ? "Stumm"
            : Peak > 0.001f
                ? "Gibt Ton aus"
                : "Audio bereit";

    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? "?"
            : DisplayName[..1].ToUpperInvariant();
}

public sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private IReadOnlyList<MMDevice> _cachedRenderDevices = [];
    private long _renderDeviceCacheTicks;

    private const double RenderDeviceCacheLifetimeMs = 2500.0;

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

    public string OutputDeviceName =>
        _device?.FriendlyName ?? "Kein Ausgabegerät";

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
        if (_device is null)
            return;

        _device.AudioEndpointVolume.MasterVolumeLevelScalar =
            Math.Clamp(value, 0f, 1f);
    }

    public void SetMasterMute(bool muted)
    {
        if (_device is null)
            return;

        _device.AudioEndpointVolume.Mute = muted;
    }

    public IReadOnlyList<AudioSourceInfo> GetAudioSources(
        IEnumerable<string>? excludedProcessNames = null)
    {
        var excluded = (excludedProcessNames ?? [])
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sources = new Dictionary<string, MutableAudioSource>(
            StringComparer.OrdinalIgnoreCase);

        foreach (MMDevice device in GetActiveRenderDevices())
        {
            try
            {
                SessionCollection sessions =
                    device.AudioSessionManager.Sessions;

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

                        float volume =
                            session.SimpleAudioVolume.Volume;

                        bool muted =
                            session.SimpleAudioVolume.Mute;

                        float peak = GetPeak(session);

                        string displayName = GetSessionDisplayName(
                            session,
                            process,
                            processName);

                        string key = processName;

                        if (!sources.TryGetValue(
                                key,
                                out MutableAudioSource? source))
                        {
                            sources[key] = new MutableAudioSource
                            {
                                ProcessId = process.Id,
                                ProcessName = processName,
                                DisplayName = displayName,
                                DeviceName = device.FriendlyName,
                                Volume = volume,
                                Muted = muted,
                                Peak = peak,
                                SessionCount = 1
                            };

                            continue;
                        }

                        source.SessionCount++;
                        source.Peak = Math.Max(source.Peak, peak);
                        source.Volume = Math.Max(source.Volume, volume);
                        source.Muted &= muted;

                        if (!source.DeviceName.Contains(
                                device.FriendlyName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            source.DeviceName +=
                                $" + {device.FriendlyName}";
                        }

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
                        // Einzelne ungültige oder geschützte Sitzung überspringen.
                    }
                }
            }
            catch
            {
                // Ein fehlerhaftes Ausgabegerät darf die restliche Suche
                // nicht verhindern.
            }
            finally
            {
                // Das Gerät bleibt für kurze Zeit im Cache.
            }
        }

        return sources.Values
            .Select(source => new AudioSourceInfo(
                source.ProcessId,
                source.ProcessName,
                source.DisplayName,
                source.DeviceName,
                source.Volume,
                source.Muted,
                source.Peak,
                source.SessionCount))
            .OrderByDescending(source => source.Peak > 0.001f)
            .ThenByDescending(source => source.Peak)
            .ThenBy(source => source.DisplayName)
            .ToList();
    }

    public bool TrySetProcessVolume(
        string processName,
        float volume)
    {
        string wanted =
            Path.GetFileNameWithoutExtension(processName);

        bool found = false;

        ForEachMatchingSession(
            wanted,
            session =>
            {
                session.SimpleAudioVolume.Volume =
                    Math.Clamp(volume, 0f, 1f);

                found = true;
            });

        return found;
    }

    public bool TrySetProcessMute(
        string processName,
        bool muted)
    {
        string wanted =
            Path.GetFileNameWithoutExtension(processName);

        bool found = false;

        ForEachMatchingSession(
            wanted,
            session =>
            {
                session.SimpleAudioVolume.Mute = muted;
                found = true;
            });

        return found;
    }

    public (float Volume, bool Muted)? TryGetProcessState(
        string processName)
    {
        string wanted =
            Path.GetFileNameWithoutExtension(processName);

        var volumes = new List<float>();
        var muteValues = new List<bool>();

        ForEachMatchingSession(
            wanted,
            session =>
            {
                volumes.Add(session.SimpleAudioVolume.Volume);
                muteValues.Add(session.SimpleAudioVolume.Mute);
            });

        if (volumes.Count == 0)
            return null;

        return (
            volumes.Max(),
            muteValues.Count > 0 && muteValues.All(value => value));
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
        IReadOnlyList<AudioSourceInfo> sources =
            GetAudioSources();

        string? foreground = GetForegroundProcessName();

        if (!string.IsNullOrWhiteSpace(foreground))
        {
            AudioSourceInfo? foregroundSource =
                sources.FirstOrDefault(source =>
                    source.ProcessName.Equals(
                        foreground,
                        StringComparison.OrdinalIgnoreCase));

            if (foregroundSource is not null)
                return foregroundSource.ProcessName;
        }

        return sources
            .Where(source =>
                !GameDetectionIgnoredProcesses.Contains(
                    source.ProcessName))
            .OrderByDescending(source => source.Peak)
            .ThenByDescending(source => source.SessionCount)
            .Select(source => source.ProcessName)
            .FirstOrDefault();
    }

    public void RefreshChannel(AppChannel channel)
    {
        string? processName =
            channel.Kind == ChannelKind.AutoGame
                ? FindBestGameAudioProcess()
                : channel.ProcessName;

        if (channel.Kind == ChannelKind.AutoGame)
            channel.ActiveTarget = processName ?? "";

        if (string.IsNullOrWhiteSpace(processName))
        {
            channel.IsAvailable = false;
            channel.StatusText =
                "Keine passende Audioquelle erkannt";

            return;
        }

        var state = TryGetProcessState(processName);

        channel.IsAvailable = state.HasValue;
        channel.StatusText =
            state.HasValue
                ? "Aktiv"
                : "Wartet auf Audio";

        if (state.HasValue)
        {
            channel.Volume = state.Value.Volume;
            channel.IsMuted = state.Value.Muted;
        }
    }

    public bool SetChannelVolume(
        AppChannel channel,
        float volume)
    {
        string? processName =
            channel.Kind == ChannelKind.AutoGame
                ? channel.ActiveTarget
                : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(processName) &&
               TrySetProcessVolume(processName, volume);
    }

    public bool SetChannelMute(
        AppChannel channel,
        bool muted)
    {
        string? processName =
            channel.Kind == ChannelKind.AutoGame
                ? channel.ActiveTarget
                : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(processName) &&
               TrySetProcessMute(processName, muted);
    }

    public void Dispose()
    {
        foreach (MMDevice cachedDevice in _cachedRenderDevices)
            cachedDevice.Dispose();

        _cachedRenderDevices = [];
        _device?.Dispose();
        _enumerator.Dispose();
    }

    private IReadOnlyList<MMDevice> GetActiveRenderDevices()
    {
        long now = Stopwatch.GetTimestamp();

        if (_cachedRenderDevices.Count > 0)
        {
            double ageMs =
                (now - _renderDeviceCacheTicks) *
                1000.0 /
                Stopwatch.Frequency;

            if (ageMs < RenderDeviceCacheLifetimeMs)
                return _cachedRenderDevices;
        }

        try
        {
            foreach (MMDevice cachedDevice in _cachedRenderDevices)
                cachedDevice.Dispose();

            MMDeviceCollection devices =
                _enumerator.EnumerateAudioEndPoints(
                    DataFlow.Render,
                    DeviceState.Active);

            _cachedRenderDevices = devices
                .Where(device => device.State == DeviceState.Active)
                .ToList();

            _renderDeviceCacheTicks = now;
            return _cachedRenderDevices;
        }
        catch
        {
            _cachedRenderDevices = [];
            return [];
        }
    }

    private void ForEachMatchingSession(
        string processName,
        Action<AudioSessionControl> action)
    {
        foreach (MMDevice device in GetActiveRenderDevices())
        {
            try
            {
                SessionCollection sessions =
                    device.AudioSessionManager.Sessions;

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

                        if (!process.ProcessName.Equals(
                                processName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        action(session);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            finally
            {
                // Das Gerät bleibt für kurze Zeit im Cache.
            }
        }
    }

    private static float GetPeak(
        AudioSessionControl session)
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
                !session.DisplayName.StartsWith(
                    "@",
                    StringComparison.Ordinal))
            {
                return session.DisplayName;
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    process.MainWindowTitle))
            {
                return process.MainWindowTitle;
            }
        }
        catch
        {
        }

        return FriendlyProcessName(fallback);
    }

    private static string FriendlyProcessName(
        string processName)
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
        public string DeviceName { get; set; } = "";
        public float Volume { get; set; }
        public bool Muted { get; set; }
        public float Peak { get; set; }
        public int SessionCount { get; set; }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);
}

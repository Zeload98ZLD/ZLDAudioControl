using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HerculesAudioControl.Models;
using NAudio.CoreAudioApi;

namespace HerculesAudioControl.Services;

public sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;

    public string OutputDeviceName => _device?.FriendlyName ?? "Kein Ausgabegerät";

    public AudioService() => RefreshDevice();

    public void RefreshDevice()
    {
        _device?.Dispose();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    public float GetMasterVolume() =>
        _device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;

    public bool GetMasterMute() =>
        _device?.AudioEndpointVolume.Mute ?? false;

    public void SetMasterVolume(float value)
    {
        if (_device is null) return;
        _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
    }

    public void SetMasterMute(bool muted)
    {
        if (_device is null) return;
        _device.AudioEndpointVolume.Mute = muted;
    }

    private IEnumerable<(AudioSessionControl Session, Process Process)> Sessions()
    {
        if (_device is null) yield break;

        SessionCollection sessions = _device.AudioSessionManager.Sessions;

        for (int i = 0; i < sessions.Count; i++)
        {
            AudioSessionControl session = sessions[i];
            Process? process = null;

            try
            {
                uint pid = session.GetProcessID;
                if (pid == 0) continue;
                process = Process.GetProcessById((int)pid);
                yield return (session, process);
            }
            finally
            {
                process?.Dispose();
            }
        }
    }

    public bool TrySetProcessVolume(string processName, float volume)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);
        bool found = false;

        foreach (var item in Sessions())
        {
            try
            {
                if (!item.Process.ProcessName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                    continue;

                item.Session.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
                found = true;
            }
            catch { }
        }

        return found;
    }

    public bool TrySetProcessMute(string processName, bool muted)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);
        bool found = false;

        foreach (var item in Sessions())
        {
            try
            {
                if (!item.Process.ProcessName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                    continue;

                item.Session.SimpleAudioVolume.Mute = muted;
                found = true;
            }
            catch { }
        }

        return found;
    }

    public (float Volume, bool Muted)? TryGetProcessState(string processName)
    {
        string wanted = Path.GetFileNameWithoutExtension(processName);

        foreach (var item in Sessions())
        {
            try
            {
                if (item.Process.ProcessName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                    return (item.Session.SimpleAudioVolume.Volume, item.Session.SimpleAudioVolume.Mute);
            }
            catch { }
        }

        return null;
    }

    public string? GetForegroundProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        try
        {
            using Process process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    public string? FindForegroundAudioProcess()
    {
        string? foreground = GetForegroundProcessName();
        if (string.IsNullOrWhiteSpace(foreground)) return null;

        foreach (var item in Sessions())
        {
            try
            {
                if (item.Process.ProcessName.Equals(foreground, StringComparison.OrdinalIgnoreCase))
                    return foreground;
            }
            catch { }
        }

        return null;
    }

    public void RefreshChannel(AppChannel channel)
    {
        string? processName = channel.Kind == ChannelKind.AutoGame
            ? FindForegroundAudioProcess()
            : channel.ProcessName;

        if (channel.Kind == ChannelKind.AutoGame)
            channel.ActiveTarget = processName ?? "";

        if (string.IsNullOrWhiteSpace(processName))
        {
            channel.IsAvailable = false;
            channel.StatusText = "Kein Spiel mit Audio erkannt";
            return;
        }

        var state = TryGetProcessState(processName);
        channel.IsAvailable = state.HasValue;
        channel.StatusText = state.HasValue ? "Aktiv" : "Wartet auf Audio";

        if (state.HasValue)
        {
            channel.Volume = state.Value.Volume;
            channel.IsMuted = state.Value.Muted;
        }
    }

    public bool SetChannelVolume(AppChannel channel, float volume)
    {
        string? process = channel.Kind == ChannelKind.AutoGame
            ? channel.ActiveTarget
            : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(process) &&
               TrySetProcessVolume(process, volume);
    }

    public bool SetChannelMute(AppChannel channel, bool muted)
    {
        string? process = channel.Kind == ChannelKind.AutoGame
            ? channel.ActiveTarget
            : channel.ProcessName;

        return !string.IsNullOrWhiteSpace(process) &&
               TrySetProcessMute(process, muted);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

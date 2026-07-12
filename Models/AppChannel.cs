using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HerculesAudioControl.Models;

public enum ChannelKind
{
    Application,
    AutoGame
}

public sealed class AppChannel : INotifyPropertyChanged
{
    private float _volume = 1f;
    private bool _isAvailable;
    private bool _isMuted;
    private string _statusText = "Wartet auf Audio";
    private string _activeTarget = "";

    public required string Id { get; init; }
    public required string ProcessName { get; init; }
    public required string DisplayName { get; init; }
    public ChannelKind Kind { get; set; } = ChannelKind.Application;
    public string Role { get; set; } = "Programm";
    public MidiBinding Midi { get; set; } = new();

    public float Volume
    {
        get => _volume;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_volume - value) < 0.001f) return;
            _volume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
        }
    }

    public int VolumePercent => (int)Math.Round(Volume * 100);

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value) return;
            _isAvailable = value;
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MuteButtonText));
        }
    }

    public string MuteButtonText => IsMuted ? "Ton an" : "Stumm";

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string ActiveTarget
    {
        get => _activeTarget;
        set
        {
            if (_activeTarget == value) return;
            _activeTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string Subtitle =>
        Kind == ChannelKind.AutoGame && !string.IsNullOrWhiteSpace(ActiveTarget)
            ? $"Aktiv: {ActiveTarget}"
            : ProcessName;

    public string MidiText => Midi.DisplayText;

    public void NotifyMidiChanged() => OnPropertyChanged(nameof(MidiText));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

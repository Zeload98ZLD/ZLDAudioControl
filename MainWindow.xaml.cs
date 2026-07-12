using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HerculesAudioControl.Models;
using HerculesAudioControl.Services;
using HerculesAudioControl.Views;

namespace HerculesAudioControl;

public partial class MainWindow : Window
{
    private readonly AudioService _audio = new();
    private readonly MidiService _midi = new();
    private readonly SettingsService _settings = new();
    private readonly EqualizerService _equalizer = new();
    private readonly UpdateService _updateService = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _learnTimer;

    private AppSettings _allSettings = new();
    private ProfileSettings _profile = new();
    private bool _updating;
    private bool _masterMuted;

    private AppChannel? _learningChannel;
    private string? _learningAction;
    private readonly List<(int Status, int Controller)> _learnedKeys = [];
    private readonly Dictionary<string, Dictionary<int, int>> _midiState = [];
    private EqualizerWindow? _equalizerWindow;

    public ObservableCollection<AppChannel> Channels { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        OutputDeviceText.Text = $"Ausgabe: {_audio.OutputDeviceName}";
        _allSettings = _settings.Load();
        ThemeService.Apply(_allSettings.Theme);
        UpdateThemeButton();
        LoadProfiles();

        Channels.CollectionChanged += (_, _) => UpdateEmptyState();
        UpdateEmptyState();
        LoadMidiDevices();

        _midi.StatusChanged += (_, text) =>
            Dispatcher.Invoke(() => MidiStatusText.Text = text);
        _midi.ControlMessageReceived += (_, message) =>
            Dispatcher.Invoke(() => HandleMidi(message));

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _refreshTimer.Tick += (_, _) => RefreshAudio();
        _refreshTimer.Start();

        _learnTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        _learnTimer.Tick += (_, _) => FinishLearning();

        RefreshAudio();
    }

    private void LoadProfiles()
    {
        if (_allSettings.Profiles.Count == 0)
        {
            _allSettings.Profiles =
            [
                new ProfileSettings { Name = "Gaming" },
                new ProfileSettings { Name = "Musik" },
                new ProfileSettings { Name = "Streaming" }
            ];
        }

        string requested = _allSettings.ActiveProfile;
        int index = requested switch { "Musik" => 1, "Streaming" => 2, _ => 0 };
        ProfileCombo.SelectedIndex = index;
        ActivateProfile(requested);
    }

    private void ActivateProfile(string name)
    {
        SaveCurrentProfile();

        _profile = _allSettings.Profiles.FirstOrDefault(p => p.Name == name)
                   ?? new ProfileSettings { Name = name };

        if (!_allSettings.Profiles.Contains(_profile))
            _allSettings.Profiles.Add(_profile);

        _allSettings.ActiveProfile = name;
        Channels.Clear();

        foreach (SavedChannel saved in _profile.Channels)
        {
            Channels.Add(new AppChannel
            {
                Id = saved.Id,
                ProcessName = saved.ProcessName,
                DisplayName = saved.DisplayName,
                Role = saved.Role,
                Kind = saved.Kind,
                Midi = saved.Midi ?? new MidiBinding()
            });
        }

        UpdateBindingTexts();
        UpdateEmptyState();
        _settings.Save(_allSettings);
    }

    private void SaveCurrentProfile()
    {
        if (_profile is null) return;

        _profile.Channels = Channels.Select(c => new SavedChannel
        {
            Id = c.Id,
            ProcessName = c.ProcessName,
            DisplayName = c.DisplayName,
            Role = c.Role,
            Kind = c.Kind,
            Midi = c.Midi
        }).ToList();

        _settings.Save(_allSettings);
    }

    private void UpdateBindingTexts()
    {
        MasterMidiText.Text = _profile.MasterMidi.DisplayText;
        MediaBindingsText.Text =
            $"Zurück: {_profile.PreviousMidi.DisplayText}  |  " +
            $"Play: {_profile.PlayPauseMidi.DisplayText}  |  " +
            $"Weiter: {_profile.NextMidi.DisplayText}";

        foreach (AppChannel c in Channels)
            c.NotifyMidiChanged();
    }

    private void LoadMidiDevices()
    {
        var devices = _midi.GetInputDevices();
        MidiDeviceCombo.ItemsSource = devices;
        string? preferred = devices.FirstOrDefault(d =>
            d.Contains("Hercules", StringComparison.OrdinalIgnoreCase));

        if (preferred is not null) MidiDeviceCombo.SelectedItem = preferred;
        else if (devices.Count > 0) MidiDeviceCombo.SelectedIndex = 0;
        else MidiStatusText.Text = "Kein MIDI-Gerät gefunden";
    }

    private void RefreshAudio()
    {
        _updating = true;
        try
        {
            float master = _audio.GetMasterVolume();
            _masterMuted = _audio.GetMasterMute();
            MasterSlider.Value = master * 100;
            MasterPercentText.Text = $"{Math.Round(master * 100)} %";
            MasterMuteButton.Content = _masterMuted ? "Ton an" : "Stumm";

            foreach (AppChannel c in Channels)
                _audio.RefreshChannel(c);
        }
        finally { _updating = false; }
    }

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow(Channels.Select(c => c.ProcessName)) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedProcess is null) return;

        var p = picker.SelectedProcess;
        var channel = new AppChannel
        {
            Id = Guid.NewGuid().ToString("N"),
            ProcessName = p.ProcessName,
            DisplayName = p.DisplayName,
            Role = p.ProcessName.Equals("Spotify", StringComparison.OrdinalIgnoreCase)
                ? "Spotify" : "Programm",
            Kind = ChannelKind.Application
        };

        _audio.RefreshChannel(channel);
        Channels.Add(channel);
        SaveCurrentProfile();
    }

    private void AddAutoGame_Click(object sender, RoutedEventArgs e)
    {
        if (Channels.Any(c => c.Kind == ChannelKind.AutoGame))
        {
            MessageBox.Show(this, "Der Auto-Game-Kanal ist bereits vorhanden.");
            return;
        }

        Channels.Add(new AppChannel
        {
            Id = Guid.NewGuid().ToString("N"),
            ProcessName = "__AUTO_GAME__",
            DisplayName = "Auto Game",
            Role = "Aktives Spiel",
            Kind = ChannelKind.AutoGame
        });

        SaveCurrentProfile();
    }

    private void LearnMaster_Click(object sender, RoutedEventArgs e) =>
        BeginLearning(null, "Master");

    private void LearnMidi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppChannel channel })
            BeginLearning(channel, null);
    }

    private void LearnAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string action })
            BeginLearning(null, action);
    }

    private void BeginLearning(AppChannel? channel, string? action)
    {
        _learningChannel = channel;
        _learningAction = action;
        _learnedKeys.Clear();
        _learnTimer.Stop();

        string target = channel?.DisplayName ?? action ?? "Aktion";
        LastMidiText.Text = $"Lernmodus: {target} – Regler oder Taste betätigen";
    }

    private void HandleMidi(MidiControlMessage m)
    {
        LastMidiText.Text = $"Letztes Signal: {m.Status}:{m.Controller} Wert {m.Value}";

        if (_learningChannel is not null || _learningAction is not null)
        {
            var key = (m.Status, m.Controller);
            if (!_learnedKeys.Contains(key)) _learnedKeys.Add(key);
            _learnTimer.Stop();
            _learnTimer.Start();
            return;
        }

        if (Matches(_profile.PlayPauseMidi, m) && m.Value > 0) MediaKeyService.PlayPause();
        if (Matches(_profile.PreviousMidi, m) && m.Value > 0) MediaKeyService.Previous();
        if (Matches(_profile.NextMidi, m) && m.Value > 0) MediaKeyService.Next();

        if (Matches(_profile.MasterMidi, m))
        {
            float volume = CalculateValue("master", _profile.MasterMidi, m);
            _audio.SetMasterVolume(volume);
        }

        HandleEqualizerMidi(m);

        foreach (AppChannel c in Channels)
        {
            if (!Matches(c.Midi, m)) continue;
            float volume = CalculateValue(c.Id, c.Midi, m);
            c.Volume = volume;
            c.IsAvailable = _audio.SetChannelVolume(c, volume);
        }
    }

    private static bool Matches(MidiBinding binding, MidiControlMessage m) =>
        binding.IsAssigned &&
        binding.Status == m.Status &&
        binding.Controllers.Contains(m.Controller);

    private float CalculateValue(string id, MidiBinding binding, MidiControlMessage m)
    {
        if (!_midiState.TryGetValue(id, out var state))
        {
            state = [];
            _midiState[id] = state;
        }

        state[m.Controller] = m.Value;

        if (binding.Controllers.Count >= 2)
        {
            int msb = state.GetValueOrDefault(binding.Controllers[0], 0);
            int lsb = state.GetValueOrDefault(binding.Controllers[1], 0);
            return ((msb << 7) | lsb) / 16383f;
        }

        return m.Value / 127f;
    }

    private void FinishLearning()
    {
        _learnTimer.Stop();
        if (_learnedKeys.Count == 0) return;

        var best = _learnedKeys.GroupBy(k => k.Status)
            .OrderByDescending(g => g.Count()).First();

        var binding = new MidiBinding
        {
            Status = best.Key,
            Controllers = best.Select(x => x.Controller).Distinct().Take(2).ToList()
        };

        if (_learningChannel is not null)
        {
            _learningChannel.Midi = binding;
            _learningChannel.NotifyMidiChanged();
            LastMidiText.Text = $"{_learningChannel.DisplayName}: {binding.DisplayText}";
        }
        else
        {
            switch (_learningAction)
            {
                case "Master": _profile.MasterMidi = binding; break;
                case "PlayPause": _profile.PlayPauseMidi = binding; break;
                case "Previous": _profile.PreviousMidi = binding; break;
                case "Next": _profile.NextMidi = binding; break;
                case "EqBass": _allSettings.Equalizer.BassMidi = binding; break;
                case "EqMid": _allSettings.Equalizer.MidMidi = binding; break;
                case "EqTreble": _allSettings.Equalizer.TrebleMidi = binding; break;
                case "EqFilter": _allSettings.Equalizer.FilterMidi = binding; break;
            }
            LastMidiText.Text = $"{_learningAction}: {binding.DisplayText}";
        }

        _learningChannel = null;
        _learningAction = null;
        _learnedKeys.Clear();
        UpdateBindingTexts();
        _equalizerWindow?.RefreshBindings();
        SaveCurrentProfile();
    }

    private void MasterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        _audio.SetMasterVolume((float)(e.NewValue / 100.0));
        MasterPercentText.Text = $"{Math.Round(e.NewValue)} %";
    }

    private void MasterMute_Click(object sender, RoutedEventArgs e)
    {
        _masterMuted = !_masterMuted;
        _audio.SetMasterMute(_masterMuted);
        MasterMuteButton.Content = _masterMuted ? "Ton an" : "Stumm";
    }

    private void ChannelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        if (sender is Slider { Tag: AppChannel c })
        {
            c.Volume = (float)e.NewValue;
            c.IsAvailable = _audio.SetChannelVolume(c, c.Volume);
        }
    }

    private void ChannelMute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppChannel c })
        {
            c.IsMuted = !c.IsMuted;
            _audio.SetChannelMute(c, c.IsMuted);
        }
    }

    private void RemoveProgram_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppChannel c })
        {
            Channels.Remove(c);
            SaveCurrentProfile();
            UpdateEmptyState();
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => MediaKeyService.PlayPause();
    private void Previous_Click(object sender, RoutedEventArgs e) => MediaKeyService.Previous();
    private void Next_Click(object sender, RoutedEventArgs e) => MediaKeyService.Next();

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ProfileCombo.SelectedItem is not ComboBoxItem item) return;
        ActivateProfile(item.Content?.ToString() ?? "Gaming");
    }

    private void MidiDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MidiDeviceCombo.SelectedItem is string name)
            _midi.Connect(name);
    }

    private void UpdateEmptyState()
    {
        if (EmptyState is null || ChannelsItems is null) return;
        EmptyState.Visibility = Channels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ChannelsItems.Visibility = Channels.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }


    private void HandleEqualizerMidi(MidiControlMessage message)
    {
        EqualizerSettings eq = _allSettings.Equalizer;
        bool changed = false;

        if (Matches(eq.BassMidi, message))
        {
            eq.BassGain = CalculateValue("eq-bass", eq.BassMidi, message) * 24 - 12;
            changed = true;
        }

        if (Matches(eq.MidMidi, message))
        {
            eq.MidGain = CalculateValue("eq-mid", eq.MidMidi, message) * 24 - 12;
            changed = true;
        }

        if (Matches(eq.TrebleMidi, message))
        {
            eq.TrebleGain = CalculateValue("eq-treble", eq.TrebleMidi, message) * 24 - 12;
            changed = true;
        }

        if (Matches(eq.FilterMidi, message))
        {
            eq.FilterPosition = CalculateValue("eq-filter", eq.FilterMidi, message) * 100;
            changed = true;
        }

        if (!changed)
            return;

        try
        {
            _equalizer.Apply(eq);
            _settings.Save(_allSettings);
        }
        catch
        {
            // Die Oberfläche zeigt beim Öffnen des EQ-Fensters den konkreten Fehler.
        }
    }

    private void OpenEqualizer_Click(object sender, RoutedEventArgs e)
    {
        if (_equalizerWindow is { IsVisible: true })
        {
            _equalizerWindow.Activate();
            return;
        }

        _equalizerWindow = new EqualizerWindow(
            _allSettings.Equalizer,
            _equalizer,
            action => BeginLearning(null, action),
            SaveAllSettings)
        {
            Owner = this
        };

        _equalizerWindow.Closed += (_, _) => _equalizerWindow = null;
        _equalizerWindow.Show();
    }

    private void OpenUpdates_Click(object sender, RoutedEventArgs e)
    {
        var window = new UpdateWindow(
            _allSettings.Update,
            _updateService,
            SaveAllSettings)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _allSettings.Theme =
            string.Equals(_allSettings.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "Light"
                : "Dark";

        ThemeService.Apply(_allSettings.Theme);
        UpdateThemeButton();
        SaveAllSettings();
    }

    private void UpdateThemeButton()
    {
        if (ThemeToggleButton is null)
            return;

        ThemeToggleButton.Content =
            string.Equals(_allSettings.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "☀ Light"
                : "☾ Dark";
    }

    private void SaveAllSettings() => _settings.Save(_allSettings);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveCurrentProfile();
        _refreshTimer.Stop();
        _learnTimer.Stop();
        _midi.Dispose();
        _audio.Dispose();
    }
}

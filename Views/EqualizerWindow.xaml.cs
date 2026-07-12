using System.Windows;
using System.Windows.Controls;
using HerculesAudioControl.Models;
using HerculesAudioControl.Services;

namespace HerculesAudioControl.Views;

public partial class EqualizerWindow : Window
{
    private readonly EqualizerSettings _settings;
    private readonly EqualizerService _service;
    private readonly Action<string> _startMidiLearning;
    private readonly Action _save;
    private bool _loading = true;

    public EqualizerWindow(
        EqualizerSettings settings,
        EqualizerService service,
        Action<string> startMidiLearning,
        Action save)
    {
        InitializeComponent();

        _settings = settings;
        _service = service;
        _startMidiLearning = startMidiLearning;
        _save = save;

        EnabledCheck.IsChecked = settings.Enabled;
        BassSlider.Value = settings.BassGain;
        MidSlider.Value = settings.MidGain;
        TrebleSlider.Value = settings.TrebleGain;
        FilterSlider.Value = settings.FilterPosition;
        OutputFileBox.Text = settings.OutputFile;

        ApoStatusText.Text = service.IsEqualizerApoLikelyInstalled()
            ? "Equalizer APO wurde gefunden."
            : "Equalizer APO wurde nicht gefunden. Der EQ kann gespeichert, aber noch nicht angewendet werden.";

        UpdateLabels();
        _loading = false;
    }

    public void RefreshBindings()
    {
        BindingsText.Text =
            $"Bass: {_settings.BassMidi.DisplayText}\n" +
            $"Mitten: {_settings.MidMidi.DisplayText}\n" +
            $"Höhen: {_settings.TrebleMidi.DisplayText}\n" +
            $"Filter: {_settings.FilterMidi.DisplayText}";
    }

    private void UpdateLabels()
    {
        BassValue.Text = $"{BassSlider.Value:+0.0;-0.0;0.0} dB";
        MidValue.Text = $"{MidSlider.Value:+0.0;-0.0;0.0} dB";
        TrebleValue.Text = $"{TrebleSlider.Value:+0.0;-0.0;0.0} dB";

        FilterValue.Text = FilterSlider.Value switch
        {
            < 47 => "Low-pass",
            > 53 => "High-pass",
            _ => "Bypass"
        };

        RefreshBindings();
    }

    private void CopyValues()
    {
        _settings.Enabled = EnabledCheck.IsChecked == true;
        _settings.BassGain = BassSlider.Value;
        _settings.MidGain = MidSlider.Value;
        _settings.TrebleGain = TrebleSlider.Value;
        _settings.FilterPosition = FilterSlider.Value;
        _settings.OutputFile = OutputFileBox.Text.Trim();
    }

    private void GainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        CopyValues();
        UpdateLabels();
        TryLiveApply();
    }

    private void EnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        CopyValues();
        TryLiveApply();
    }

    private void OutputFileChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _settings.OutputFile = OutputFileBox.Text.Trim();
    }

    private void TryLiveApply()
    {
        try
        {
            _service.Apply(_settings);
            _save();
            StatusText.Text = _settings.Enabled
                ? "EQ live angewendet."
                : "EQ ist deaktiviert.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"EQ konnte nicht geschrieben werden: {ex.Message}";
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        CopyValues();
        TryLiveApply();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        BassSlider.Value = 0;
        MidSlider.Value = 0;
        TrebleSlider.Value = 0;
        FilterSlider.Value = 50;
        CopyValues();
        TryLiveApply();
    }

    private void LearnMidi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string action })
        {
            _startMidiLearning(action);
            StatusText.Text = "Lernmodus aktiv: Regler am Hercules bewegen und kurz warten.";
        }
    }
}

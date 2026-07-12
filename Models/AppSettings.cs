namespace HerculesAudioControl.Models;

public sealed class AppSettings
{
    public string ActiveProfile { get; set; } = "Gaming";
    public string Theme { get; set; } = "Dark";
    public UpdateSettings Update { get; set; } = new();
    public EqualizerSettings Equalizer { get; set; } = new();
    public List<ProfileSettings> Profiles { get; set; } = [];
}

public sealed class UpdateSettings
{
    public string GitHubOwner { get; set; } = "";
    public string GitHubRepository { get; set; } = "";
    public bool CheckOnStartup { get; set; } = true;
}

public sealed class EqualizerSettings
{
    public bool Enabled { get; set; }
    public string OutputFile { get; set; } =
        @"C:\Program Files\EqualizerAPO\config\hercules_dynamic.txt";

    public double BassGain { get; set; }
    public double MidGain { get; set; }
    public double TrebleGain { get; set; }
    public double FilterPosition { get; set; } = 50;

    public MidiBinding BassMidi { get; set; } = new();
    public MidiBinding MidMidi { get; set; } = new();
    public MidiBinding TrebleMidi { get; set; } = new();
    public MidiBinding FilterMidi { get; set; } = new();
}

public sealed class ProfileSettings
{
    public string Name { get; set; } = "Gaming";
    public MidiBinding MasterMidi { get; set; } = new();
    public MidiBinding PlayPauseMidi { get; set; } = new();
    public MidiBinding PreviousMidi { get; set; } = new();
    public MidiBinding NextMidi { get; set; } = new();
    public List<SavedChannel> Channels { get; set; } = [];
}

public sealed class SavedChannel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProcessName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Programm";
    public ChannelKind Kind { get; set; }
    public MidiBinding Midi { get; set; } = new();
}

using NAudio.Midi;

namespace HerculesAudioControl.Services;

public sealed record MidiControlMessage(int Status, int Controller, int Value);

public sealed class MidiService : IDisposable
{
    private MidiIn? _midiIn;

    public event EventHandler<MidiControlMessage>? ControlMessageReceived;
    public event EventHandler<string>? StatusChanged;

    public IReadOnlyList<string> GetInputDevices()
    {
        var result = new List<string>();

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            result.Add(MidiIn.DeviceInfo(i).ProductName);

        return result;
    }

    public bool Connect(string deviceName)
    {
        Disconnect();

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            MidiInCapabilities info = MidiIn.DeviceInfo(i);

            if (!string.Equals(info.ProductName, deviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            _midiIn = new MidiIn(i);
            _midiIn.MessageReceived += OnMessageReceived;
            _midiIn.ErrorReceived += (_, e) =>
                StatusChanged?.Invoke(this, $"MIDI-Fehler: {e.RawMessage}");
            _midiIn.Start();

            StatusChanged?.Invoke(this, $"Verbunden: {deviceName}");
            return true;
        }

        StatusChanged?.Invoke(this, "MIDI-Gerät nicht gefunden");
        return false;
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        int status = e.RawMessage & 0xFF;
        int data1 = (e.RawMessage >> 8) & 0xFF;
        int data2 = (e.RawMessage >> 16) & 0xFF;

        ControlMessageReceived?.Invoke(
            this,
            new MidiControlMessage(status, data1, data2));
    }

    public void Disconnect()
    {
        if (_midiIn is null) return;

        _midiIn.Stop();
        _midiIn.Dispose();
        _midiIn = null;
        StatusChanged?.Invoke(this, "Nicht verbunden");
    }

    public void Dispose() => Disconnect();
}

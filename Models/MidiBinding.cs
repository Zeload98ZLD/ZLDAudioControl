namespace HerculesAudioControl.Models;

public sealed class MidiBinding
{
    public int? Status { get; set; }
    public List<int> Controllers { get; set; } = [];

    public bool IsAssigned => Status.HasValue && Controllers.Count > 0;

    public string DisplayText =>
        !IsAssigned
            ? "Nicht belegt"
            : string.Join(" + ", Controllers.Select(c => $"{Status}:{c}"));
}

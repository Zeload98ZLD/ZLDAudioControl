using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HerculesAudioControl.Views;

public partial class ProcessPickerWindow : Window
{
    private readonly List<ProcessChoice> _allProcesses = [];

    private static readonly Dictionary<string, (string ProcessName, string DisplayName)> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Spotify"] = ("Spotify", "Spotify"),
            ["Discord"] = ("Discord", "Discord"),
            ["chrome"] = ("chrome", "Google Chrome"),
            ["firefox"] = ("firefox", "Mozilla Firefox"),
            ["obs64"] = ("obs64", "OBS Studio"),
            ["vlc"] = ("vlc", "VLC media player"),
            ["ms-teams"] = ("ms-teams", "Microsoft Teams")
        };

    public ProcessChoice? SelectedProcess { get; private set; }

    public ProcessPickerWindow(IEnumerable<string> excludedProcessNames)
    {
        InitializeComponent();

        var excluded = excludedProcessNames
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Process process in Process.GetProcesses()
                     .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (excluded.Contains(process.ProcessName))
                    continue;

                if (!seen.Add(process.ProcessName))
                    continue;

                string title = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                    ? process.ProcessName
                    : process.MainWindowTitle;

                _allProcesses.Add(new ProcessChoice(
                    process.Id,
                    process.ProcessName,
                    title));
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        ApplyFilter("");
    }

    private void ApplyFilter(string query)
    {
        IEnumerable<ProcessChoice> filtered = _allProcesses;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(p =>
                p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        ProcessList.ItemsSource = filtered.ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyFilter(SearchBox.Text);

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key })
            return;

        if (!Presets.TryGetValue(key, out var preset))
            return;

        ProcessChoice? running = _allProcesses.FirstOrDefault(p =>
            p.ProcessName.Equals(preset.ProcessName, StringComparison.OrdinalIgnoreCase));

        SelectedProcess = running ?? new ProcessChoice(
            0,
            preset.ProcessName,
            preset.DisplayName);

        DialogResult = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void ProcessList_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        ConfirmSelection();

    private void ConfirmSelection()
    {
        if (ProcessList.SelectedItem is not ProcessChoice process)
        {
            MessageBox.Show(this, "Bitte zuerst ein Programm auswählen.");
            return;
        }

        SelectedProcess = process;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}

public sealed record ProcessChoice(int Id, string ProcessName, string DisplayName)
{
    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? "?"
            : DisplayName[..1].ToUpperInvariant();
}

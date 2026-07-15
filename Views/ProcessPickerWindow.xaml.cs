using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HerculesAudioControl.Services;

namespace HerculesAudioControl.Views;

public partial class ProcessPickerWindow : Window
{
    private readonly AudioService _audioService;
    private readonly HashSet<string> _excludedProcessNames;
    private List<AudioSourceInfo> _allSources = [];

    public AudioSourceInfo? SelectedProcess { get; private set; }

    public ProcessPickerWindow(
        AudioService audioService,
        IEnumerable<string> excludedProcessNames)
    {
        InitializeComponent();

        _audioService = audioService;
        _excludedProcessNames = excludedProcessNames
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RefreshSources();
    }

    private void RefreshSources()
    {
        _allSources = _audioService
            .GetAudioSources(_excludedProcessNames)
            .ToList();

        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        IEnumerable<AudioSourceInfo> filtered = _allSources;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(source =>
                source.ProcessName.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase) ||
                source.DisplayName.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase));
        }

        List<AudioSourceInfo> result = filtered.ToList();
        ProcessList.ItemsSource = result;

        SourceCountText.Text =
            $"{result.Count} von {_allSources.Count} Audioquellen";

        bool empty = result.Count == 0;

        EmptyState.Visibility = empty
            ? Visibility.Visible
            : Visibility.Collapsed;

        ProcessList.Visibility = empty
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        RefreshSources();

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e) =>
        ApplyFilter(SearchBox.Text);

    private void Add_Click(
        object sender,
        RoutedEventArgs e) =>
        ConfirmSelection();

    private void ProcessList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e) =>
        ConfirmSelection();

    private void ConfirmSelection()
    {
        if (ProcessList.SelectedItem is not AudioSourceInfo source)
        {
            MessageBox.Show(
                this,
                "Bitte zuerst eine Audioquelle auswählen.",
                "ZLD Audio Control",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        SelectedProcess = source;
        DialogResult = true;
    }

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e) =>
        DialogResult = false;
}


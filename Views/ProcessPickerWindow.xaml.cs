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
    private bool _isLoading;

    public AudioSourceInfo? SelectedProcess { get; private set; }

    public ProcessPickerWindow(
        AudioService audioService,
        IEnumerable<string> excludedProcessNames)
    {
        InitializeComponent();

        _audioService = audioService;
        _excludedProcessNames = excludedProcessNames
            .Select(name => Path.GetFileNameWithoutExtension(name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Loaded += (_, _) => RefreshSourcesSafely();
    }

    private void RefreshSourcesSafely()
    {
        if (_isLoading)
            return;

        _isLoading = true;

        try
        {
            SourceCountText.Text = "Audioquellen werden gesucht …";

            _allSources = _audioService
                .GetAudioSources(_excludedProcessNames)
                .ToList();

            ApplyFilter(SearchBox.Text);
        }
        catch (Exception exception)
        {
            _allSources = [];
            ProcessList.ItemsSource = null;
            ProcessList.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            SourceCountText.Text = "Fehler beim Auslesen";

            MessageBox.Show(
                this,
                "Die Windows-Audioquellen konnten nicht vollständig " +
                "ausgelesen werden.\n\n" +
                exception.Message +
                "\n\nDie Anwendung bleibt geöffnet. Starte eine " +
                "Audioausgabe und versuche anschließend erneut zu aktualisieren.",
                "ZLD Audio Control – Audioquellen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFilter(string? query)
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

    private void Refresh_Click(
        object sender,
        RoutedEventArgs e) =>
        RefreshSourcesSafely();

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_isLoading)
            ApplyFilter(SearchBox.Text);
    }

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

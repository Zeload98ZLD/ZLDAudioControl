using System.Windows;
using HerculesAudioControl.Models;
using HerculesAudioControl.Services;

namespace HerculesAudioControl.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateSettings _settings;
    private readonly UpdateService _service;
    private readonly Action _save;

    private UpdateCheckResult? _lastResult;

    public UpdateWindow(
        UpdateSettings settings,
        UpdateService service,
        Action save)
    {
        InitializeComponent();

        _settings = settings;
        _service = service;
        _save = save;

        CurrentVersionText.Text =
            $"Installierte Version: {UpdateService.CurrentVersion}";

        OwnerBox.Text = settings.GitHubOwner;
        RepositoryBox.Text = settings.GitHubRepository;
        StartupCheck.IsChecked = settings.CheckOnStartup;
    }

    private void CopySettings()
    {
        _settings.GitHubOwner = OwnerBox.Text.Trim();
        _settings.GitHubRepository = RepositoryBox.Text.Trim();
        _settings.CheckOnStartup = StartupCheck.IsChecked == true;
        _save();
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        CopySettings();
        SetBusy(true);

        StatusText.Text = "Update wird geprüft …";
        OpenReleaseButton.Visibility = Visibility.Collapsed;
        InstallUpdateButton.Visibility = Visibility.Collapsed;
        DownloadProgress.Visibility = Visibility.Collapsed;
        ProgressText.Visibility = Visibility.Collapsed;

        try
        {
            _lastResult = await _service.CheckAsync(_settings);

            StatusText.Text =
                _lastResult.IsConfigured
                    ? $"{_lastResult.Message}\n" +
                      $"Installiert: {_lastResult.CurrentVersion}" +
                      (string.IsNullOrWhiteSpace(_lastResult.LatestVersion)
                          ? ""
                          : $"\nVerfügbar: {_lastResult.LatestVersion}")
                    : _lastResult.Message;

            OpenReleaseButton.Visibility =
                !string.IsNullOrWhiteSpace(_lastResult.ReleaseUrl)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            InstallUpdateButton.Visibility =
                _lastResult.UpdateAvailable &&
                !string.IsNullOrWhiteSpace(_lastResult.AssetDownloadUrl)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null ||
            !_lastResult.UpdateAvailable ||
            string.IsNullOrWhiteSpace(_lastResult.AssetDownloadUrl))
        {
            MessageBox.Show(
                this,
                "Es wurde kein installierbares Update gefunden.",
                "ZLD Audio Control",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"ZLD Audio Control {_lastResult.LatestVersion} herunterladen " +
            "und installieren?\n\n" +
            "Die Anwendung wird danach automatisch neu gestartet.",
            "Update installieren",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
            return;

        SetBusy(true);
        InstallUpdateButton.IsEnabled = false;
        OpenReleaseButton.IsEnabled = false;
        DownloadProgress.Value = 0;
        DownloadProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;
        StatusText.Text = "Update wird heruntergeladen …";

        var progress = new Progress<double>(value =>
        {
            DownloadProgress.Value = value;
            ProgressText.Text = $"{value:0} %";
        });

        try
        {
            string downloadedExecutable =
                await _service.DownloadUpdateAsync(
                    _lastResult,
                    progress);

            StatusText.Text =
                "Download abgeschlossen. ZLD Audio Control wird neu gestartet …";

            _service.StartInstallerAndRestart(downloadedExecutable);

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Das Update konnte nicht installiert werden: {ex.Message}";

            InstallUpdateButton.IsEnabled = true;
            OpenReleaseButton.IsEnabled = true;
            SetBusy(false);
        }
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastResult?.ReleaseUrl))
            UpdateService.OpenReleasePage(_lastResult.ReleaseUrl);
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        CopySettings();
        Close();
    }

    private void SetBusy(bool busy)
    {
        CheckButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
        OwnerBox.IsEnabled = !busy;
        RepositoryBox.IsEnabled = !busy;
        StartupCheck.IsEnabled = !busy;

        if (!busy)
        {
            OpenReleaseButton.IsEnabled = true;
            InstallUpdateButton.IsEnabled = true;
        }
    }
}

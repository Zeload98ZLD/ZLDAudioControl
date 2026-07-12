using System.Windows;
using HerculesAudioControl.Models;
using HerculesAudioControl.Services;

namespace HerculesAudioControl.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateSettings _settings;
    private readonly UpdateService _service;
    private readonly Action _save;
    private string? _releaseUrl;

    public UpdateWindow(
        UpdateSettings settings,
        UpdateService service,
        Action save)
    {
        InitializeComponent();
        _settings = settings;
        _service = service;
        _save = save;

        CurrentVersionText.Text = $"Installierte Version: {UpdateService.CurrentVersion}";
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
        StatusText.Text = "Update wird geprüft …";
        OpenReleaseButton.Visibility = Visibility.Collapsed;

        UpdateCheckResult result = await _service.CheckAsync(_settings);
        StatusText.Text =
            result.IsConfigured
                ? $"{result.Message}\nInstalliert: {result.CurrentVersion}" +
                  (string.IsNullOrWhiteSpace(result.LatestVersion)
                      ? ""
                      : $"\nVerfügbar: {result.LatestVersion}")
                : result.Message;

        _releaseUrl = result.ReleaseUrl;
        OpenReleaseButton.Visibility =
            !string.IsNullOrWhiteSpace(_releaseUrl)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_releaseUrl))
            UpdateService.OpenReleasePage(_releaseUrl);
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        CopySettings();
        Close();
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using HerculesAudioControl.Models;

namespace HerculesAudioControl.Services;

public sealed record UpdateCheckResult(
    bool IsConfigured,
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string Message,
    string? ReleaseUrl);

public sealed class UpdateService
{
    private readonly HttpClient _client = new();

    public UpdateService()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ZLDAudioControl", CurrentVersion));
        _client.Timeout = TimeSpan.FromSeconds(12);
    }

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.3";

    public async Task<UpdateCheckResult> CheckAsync(UpdateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.GitHubOwner) ||
            string.IsNullOrWhiteSpace(settings.GitHubRepository))
        {
            return new UpdateCheckResult(
                false, false, CurrentVersion, "",
                "Für Updates muss später ein GitHub-Repository hinterlegt werden.",
                null);
        }

        string url =
            $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepository}/releases/latest";

        try
        {
            GitHubRelease? release =
                await _client.GetFromJsonAsync<GitHubRelease>(url);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult(
                    true, false, CurrentVersion, "",
                    "GitHub hat keine gültige Release-Version geliefert.",
                    null);
            }

            string normalized = release.TagName.TrimStart('v', 'V');
            bool newer =
                Version.TryParse(normalized, out Version? latest) &&
                Version.TryParse(CurrentVersion, out Version? current) &&
                latest > current;

            return new UpdateCheckResult(
                true,
                newer,
                CurrentVersion,
                normalized,
                newer
                    ? $"Version {normalized} ist verfügbar."
                    : "Du verwendest bereits die aktuelle Version.",
                release.HtmlUrl);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                true, false, CurrentVersion, "",
                $"Update-Prüfung fehlgeschlagen: {ex.Message}",
                null);
        }
    }

    public static void OpenReleasePage(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";
    }
}

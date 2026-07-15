using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using HerculesAudioControl.Models;

namespace HerculesAudioControl.Services;

public sealed record UpdateCheckResult(
    bool IsConfigured,
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string Message,
    string? ReleaseUrl,
    string? AssetDownloadUrl,
    string? AssetName);

public sealed class UpdateService
{
    private readonly HttpClient _client = new();

    public UpdateService()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ZLDAudioControl", CurrentVersion));
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.5.6";

    public async Task<UpdateCheckResult> CheckAsync(UpdateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.GitHubOwner) ||
            string.IsNullOrWhiteSpace(settings.GitHubRepository))
        {
            return new UpdateCheckResult(
                false,
                false,
                CurrentVersion,
                "",
                "Trage GitHub-Inhaber und Repository-Name ein.",
                null,
                null,
                null);
        }

        string url =
            $"https://api.github.com/repos/{settings.GitHubOwner}/" +
            $"{settings.GitHubRepository}/releases/latest";

        try
        {
            GitHubRelease? release =
                await _client.GetFromJsonAsync<GitHubRelease>(url);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult(
                    true,
                    false,
                    CurrentVersion,
                    "",
                    "GitHub hat keine gültige Release-Version geliefert.",
                    null,
                    null,
                    null);
            }

            string normalized = release.TagName.TrimStart('v', 'V');

            bool newer =
                Version.TryParse(normalized, out Version? latest) &&
                Version.TryParse(CurrentVersion, out Version? current) &&
                latest > current;

            GitHubAsset? executable = release.Assets
                .Where(asset =>
                    asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(asset =>
                    asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(asset =>
                    asset.Name.Contains("ZLD", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            string message;

            if (!newer)
            {
                message = "Du verwendest bereits die aktuelle Version.";
            }
            else if (executable is null)
            {
                message =
                    $"Version {normalized} ist verfügbar, aber das Release " +
                    "enthält keine installierbare EXE.";
            }
            else
            {
                message = $"Version {normalized} ist verfügbar.";
            }

            return new UpdateCheckResult(
                true,
                newer,
                CurrentVersion,
                normalized,
                message,
                release.HtmlUrl,
                executable?.BrowserDownloadUrl,
                executable?.Name);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                true,
                false,
                CurrentVersion,
                "",
                $"Update-Prüfung fehlgeschlagen: {ex.Message}",
                null,
                null,
                null);
        }
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.AssetDownloadUrl))
            throw new InvalidOperationException(
                "Für dieses Release wurde keine EXE gefunden.");

        string updateDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZLDAudioControl",
            "Updates",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(updateDirectory);

        string safeName = string.IsNullOrWhiteSpace(update.AssetName)
            ? $"ZLDAudioControl-{update.LatestVersion}.exe"
            : Path.GetFileName(update.AssetName);

        string destination = Path.Combine(updateDirectory, safeName);

        using HttpResponseMessage response = await _client.GetAsync(
            update.AssetDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using Stream source =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream target = new(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        byte[] buffer = new byte[81920];
        long downloaded = 0;

        while (true)
        {
            int read = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);

            if (read == 0)
                break;

            await target.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);

            downloaded += read;

            if (totalBytes is > 0)
                progress?.Report(downloaded * 100.0 / totalBytes.Value);
        }

        progress?.Report(100);
        return destination;
    }

    public void StartInstallerAndRestart(string downloadedExecutable)
    {
        string? currentExecutable = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(currentExecutable) ||
            !File.Exists(currentExecutable))
        {
            throw new InvalidOperationException(
                "Der Pfad der laufenden Anwendung konnte nicht bestimmt werden.");
        }

        string scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"ZLD-Audio-Control-Updater-{Guid.NewGuid():N}.cmd");

        int currentPid = Environment.ProcessId;
        string downloadDirectory =
            Path.GetDirectoryName(downloadedExecutable) ?? Path.GetTempPath();

        string script = BuildUpdaterScript(
            currentPid,
            downloadedExecutable,
            currentExecutable,
            downloadDirectory,
            scriptPath);

        File.WriteAllText(
            scriptPath,
            script,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        bool elevationRequired =
            !CanWriteToDirectory(
                Path.GetDirectoryName(currentExecutable) ?? "");

        var startInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (elevationRequired)
            startInfo.Verb = "runas";

        Process.Start(startInfo);
    }

    public static void OpenReleasePage(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private static string BuildUpdaterScript(
        int processId,
        string downloadedExecutable,
        string currentExecutable,
        string downloadDirectory,
        string scriptPath)
    {
        return $"""
@echo off
setlocal
set "ZLD_PID={processId}"
set "ZLD_NEW={downloadedExecutable}"
set "ZLD_TARGET={currentExecutable}"
set "ZLD_TEMP={downloadDirectory}"

:wait_for_zld
tasklist /FI "PID eq %ZLD_PID%" 2>NUL | find "%ZLD_PID%" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto wait_for_zld
)

copy /Y "%ZLD_NEW%" "%ZLD_TARGET%" >NUL
if errorlevel 1 (
    echo ZLD Audio Control konnte nicht aktualisiert werden.
    echo Neue Datei: %ZLD_NEW%
    echo Ziel: %ZLD_TARGET%
    pause
    exit /b 1
)

start "" "%ZLD_TARGET%"
del /Q "%ZLD_NEW%" >NUL 2>&1
rmdir /S /Q "%ZLD_TEMP%" >NUL 2>&1
del /Q "{scriptPath}" >NUL 2>&1
endlocal
""";
    }

    private static bool CanWriteToDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        string probe = Path.Combine(
            directory,
            $".zld-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}

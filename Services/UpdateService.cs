using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace PlustekBCR.Services;

public interface IUpdateService
{
    Task CheckForUpdatesAsync(XamlRoot? xamlRoot);
}

public class UpdateService : IUpdateService
{
    private readonly UpdateOptions _options;

    public UpdateService(IConfiguration configuration)
    {
        _options = configuration.GetSection("Update").Get<UpdateOptions>() ?? new UpdateOptions();
    }

    public async Task CheckForUpdatesAsync(XamlRoot? xamlRoot)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ManifestUrl) || xamlRoot == null)
        {
            WriteLog($"Skip check. Enabled={_options.Enabled}, ManifestUrlEmpty={string.IsNullOrWhiteSpace(_options.ManifestUrl)}, XamlRootNull={xamlRoot == null}");
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_options.CheckTimeoutSeconds) };
            var json = await http.GetStringAsync(_options.ManifestUrl);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                WriteLog("Manifest is null or missing required fields.");
                return;
            }

            if (!Version.TryParse(manifest.Version, out var latestVersion))
            {
                WriteLog($"Invalid manifest version: {manifest.Version}");
                return;
            }

            var currentVersion = GetCurrentVersion();
            WriteLog($"Check complete. Current={currentVersion}, Latest={latestVersion}, DownloadUrl={manifest.DownloadUrl}");
            if (latestVersion <= currentVersion)
            {
                return;
            }

            var content = $"Current version: {currentVersion}\nLatest version: {latestVersion}";
            if (!string.IsNullOrWhiteSpace(manifest.Notes))
            {
                content += $"\n\nRelease notes:\n{manifest.Notes}";
            }

            var dialog = new ContentDialog
            {
                Title = "Update available",
                Content = content,
                PrimaryButtonText = "Update now",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri(manifest.DownloadUrl));
            }
        }
        catch (Exception ex)
        {
            WriteLog($"Exception: {ex}");
        }
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version ?? new Version(1, 0, 0, 0);
    }

    private static void WriteLog(string message)
    {
        try
        {
            File.AppendAllText("update_check_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // ignored
        }
    }
}

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
                return;
            }

            if (!Version.TryParse(manifest.Version, out var latestVersion))
            {
                return;
            }

            var currentVersion = GetCurrentVersion();
            if (latestVersion <= currentVersion)
            {
                return;
            }

            var content = $"目前版本：{currentVersion}\n最新版本：{latestVersion}";
            if (!string.IsNullOrWhiteSpace(manifest.Notes))
            {
                content += $"\n\n更新內容：\n{manifest.Notes}";
            }

            var dialog = new ContentDialog
            {
                Title = "有可用更新",
                Content = content,
                PrimaryButtonText = "前往更新",
                CloseButtonText = "稍後",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri(manifest.DownloadUrl));
            }
        }
        catch
        {
            // Ignore update check errors to avoid blocking app startup.
        }
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version ?? new Version(1, 0, 0, 0);
    }
}

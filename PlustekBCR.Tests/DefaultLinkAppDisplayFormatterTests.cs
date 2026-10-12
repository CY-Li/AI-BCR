using PlustekBCR.ViewModels;
using PlustekBCR.Services;
using System.Globalization;
using Xunit;

namespace PlustekBCR.Tests;

public class DefaultLinkAppDisplayFormatterTests
{
    [Fact]
    public void WebHandler_WhenHttpAndHttpsMatch_ShowsOneApplication()
    {
        var result = DefaultLinkAppDisplayFormatter.FormatWeb(
            "Microsoft Edge",
            "Microsoft Edge",
            (http, https) => $"HTTP: {http}; HTTPS: {https}");

        Assert.Equal("Microsoft Edge", result);
    }

    [Fact]
    public void WebHandler_WhenHttpAndHttpsDiffer_ShowsBothApplications()
    {
        var result = DefaultLinkAppDisplayFormatter.FormatWeb(
            "Browser A",
            "Browser B",
            (http, https) => $"HTTP: {http}; HTTPS: {https}");

        Assert.Equal("HTTP: Browser A; HTTPS: Browser B", result);
    }
}

public class DefaultLinkAppsViewModelTests
{
    [Fact]
    public async Task Refresh_MapsAvailableMissingAndMixedHandlers()
    {
        var service = new FakeDefaultLinkAppService
        {
            Snapshot = new DefaultLinkAppsSnapshot(
                new("tel", DefaultLinkAppState.Available, "Microsoft Teams"),
                new("mailto", DefaultLinkAppState.NotConfigured),
                new("http", DefaultLinkAppState.Available, "Browser A"),
                new("https", DefaultLinkAppState.Available, "Browser B"))
        };
        var viewModel = new DefaultLinkAppsViewModel(service, new FakeLocalizationService());

        await viewModel.RefreshAsync();

        Assert.Equal("Microsoft Teams", viewModel.PhoneHandlerDisplay);
        Assert.Equal("Not configured", viewModel.EmailHandlerDisplay);
        Assert.Equal("Browser A", viewModel.HttpHandlerDisplay);
        Assert.Equal("Browser B", viewModel.HttpsHandlerDisplay);
        Assert.Equal("HTTP: Browser A; HTTPS: Browser B", viewModel.WebHandlerDisplay);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Refresh_WhenQueryFails_ShowsSafeErrorState()
    {
        var service = new FakeDefaultLinkAppService { QueryException = new InvalidOperationException() };
        var viewModel = new DefaultLinkAppsViewModel(service, new FakeLocalizationService());

        await viewModel.RefreshAsync();

        Assert.Equal("Unable to determine", viewModel.PhoneHandlerDisplay);
        Assert.Equal("Query failed", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Refresh_WhileAlreadyRunning_DoesNotStartDuplicateQuery()
    {
        var service = new FakeDefaultLinkAppService { PauseQuery = true };
        var viewModel = new DefaultLinkAppsViewModel(service, new FakeLocalizationService());

        var firstRefresh = viewModel.RefreshAsync();
        await service.QueryStarted.Task;
        await viewModel.RefreshAsync();
        service.ReleaseQuery.SetResult();
        await firstRefresh;

        Assert.Equal(1, service.QueryCount);
    }

    [Fact]
    public async Task OpenWindowsSettings_WhenLaunchFails_ShowsInlineError()
    {
        var service = new FakeDefaultLinkAppService { OpenSettingsResult = false };
        var viewModel = new DefaultLinkAppsViewModel(service, new FakeLocalizationService());

        await viewModel.OpenWindowsSettingsCommand.ExecuteAsync(null);

        Assert.Equal("Open failed", viewModel.ErrorMessage);
    }

    private sealed class FakeDefaultLinkAppService : IDefaultLinkAppService
    {
        public DefaultLinkAppsSnapshot Snapshot { get; set; } = new(
            new("tel", DefaultLinkAppState.NotConfigured),
            new("mailto", DefaultLinkAppState.NotConfigured),
            new("http", DefaultLinkAppState.NotConfigured),
            new("https", DefaultLinkAppState.NotConfigured));

        public Exception? QueryException { get; set; }
        public bool PauseQuery { get; set; }
        public bool OpenSettingsResult { get; set; } = true;
        public int QueryCount { get; private set; }
        public TaskCompletionSource QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseQuery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DefaultLinkAppsSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            QueryCount++;
            QueryStarted.TrySetResult();
            if (PauseQuery)
            {
                await ReleaseQuery.Task.WaitAsync(cancellationToken);
            }

            if (QueryException != null)
            {
                throw QueryException;
            }

            return Snapshot;
        }

        public Task<bool> OpenWindowsDefaultAppsSettingsAsync() => Task.FromResult(OpenSettingsResult);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["Settings.LinkApps.NotConfigured"] = "Not configured",
            ["Settings.LinkApps.Unavailable"] = "Unable to determine",
            ["Settings.LinkApps.QueryFailed"] = "Query failed",
            ["Settings.LinkApps.OpenFailed"] = "Open failed",
            ["Settings.LinkApps.Web.Mixed"] = "HTTP: {0}; HTTPS: {1}"
        };

        public string CurrentLanguageTag => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo(CurrentLanguageTag);
        public IReadOnlyList<string> SupportedLanguageTags { get; } = new[] { "en-US" };
        public event Action? LanguageChanged
        {
            add { }
            remove { }
        }
        public string GetString(string key) => Values[key];
        public string Format(string key, params object[] args) => string.Format(CurrentCulture, GetString(key), args);
        public Task SetLanguageAsync(string languageTag) => Task.CompletedTask;
    }
}

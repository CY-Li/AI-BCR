using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels
{
    public sealed partial class DefaultLinkAppsViewModel : ObservableObject
    {
        private readonly IDefaultLinkAppService _defaultLinkAppService;
        private readonly ILocalizationService _localizationService;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private DefaultLinkAppsSnapshot? _snapshot;

        [ObservableProperty]
        public partial string PhoneHandlerDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string EmailHandlerDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string WebHandlerDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HttpHandlerDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HttpsHandlerDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public DefaultLinkAppsViewModel(
            IDefaultLinkAppService defaultLinkAppService,
            ILocalizationService localizationService)
        {
            _defaultLinkAppService = defaultLinkAppService;
            _localizationService = localizationService;
            _localizationService.LanguageChanged += ApplySnapshot;
            ApplySnapshot();
        }

        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (!await _refreshGate.WaitAsync(0))
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                _snapshot = await _defaultLinkAppService.GetCurrentAsync();
                ApplySnapshot();
            }
            catch
            {
                _snapshot = null;
                ApplySnapshot();
                ErrorMessage = _localizationService.GetString("Settings.LinkApps.QueryFailed");
            }
            finally
            {
                IsBusy = false;
                _refreshGate.Release();
            }
        }

        [RelayCommand]
        private async Task OpenWindowsSettingsAsync()
        {
            ErrorMessage = string.Empty;
            if (!await _defaultLinkAppService.OpenWindowsDefaultAppsSettingsAsync())
            {
                ErrorMessage = _localizationService.GetString("Settings.LinkApps.OpenFailed");
            }
        }

        private void ApplySnapshot()
        {
            PhoneHandlerDisplay = FormatHandler(_snapshot?.Phone);
            EmailHandlerDisplay = FormatHandler(_snapshot?.Email);

            var httpDisplay = FormatHandler(_snapshot?.Http);
            var httpsDisplay = FormatHandler(_snapshot?.Https);
            HttpHandlerDisplay = httpDisplay;
            HttpsHandlerDisplay = httpsDisplay;
            WebHandlerDisplay = DefaultLinkAppDisplayFormatter.FormatWeb(
                httpDisplay,
                httpsDisplay,
                (http, https) => _localizationService.Format("Settings.LinkApps.Web.Mixed", http, https));
        }

        private string FormatHandler(DefaultLinkAppInfo? handler) => handler?.State switch
        {
            DefaultLinkAppState.Available when !string.IsNullOrWhiteSpace(handler.DisplayName) => handler.DisplayName,
            DefaultLinkAppState.NotConfigured => _localizationService.GetString("Settings.LinkApps.NotConfigured"),
            _ => _localizationService.GetString("Settings.LinkApps.Unavailable")
        };
    }

    public static class DefaultLinkAppDisplayFormatter
    {
        public static string FormatWeb(
            string httpDisplay,
            string httpsDisplay,
            Func<string, string, string> mixedFormatter) =>
            string.Equals(httpDisplay, httpsDisplay, StringComparison.OrdinalIgnoreCase)
                ? httpDisplay
                : mixedFormatter(httpDisplay, httpsDisplay);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels
{
    public sealed partial class ScanSettingsViewModel : ObservableObject
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILocalizationService _localizationService;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private bool _isLoading;

        [ObservableProperty]
        public partial ScanResolutionOption? SelectedResolutionOption { get; set; }

        [ObservableProperty]
        public partial ScanColorModeOption? SelectedColorModeOption { get; set; }

        [ObservableProperty]
        public partial bool AutoCrop { get; set; }

        [ObservableProperty]
        public partial bool AutoDeskew { get; set; }

        [ObservableProperty]
        public partial bool AutoRotate { get; set; }

        [ObservableProperty]
        public partial string SaveErrorMessage { get; set; } = string.Empty;

        public ObservableCollection<ScanResolutionOption> ResolutionOptions { get; } = new();
        public ObservableCollection<ScanColorModeOption> ColorModeOptions { get; } = new();
        public bool HasSaveError => !string.IsNullOrWhiteSpace(SaveErrorMessage);

        public ScanSettingsViewModel(
            IApplicationSettingsService settingsService,
            ILocalizationService localizationService)
        {
            _settingsService = settingsService;
            _localizationService = localizationService;
            _localizationService.LanguageChanged += Reload;
            Reload();
        }

        partial void OnSelectedResolutionOptionChanged(ScanResolutionOption? value)
        {
            foreach (var option in ResolutionOptions)
            {
                option.IsSelected = ReferenceEquals(option, value);
            }

            QueueSave(value != null);
        }

        partial void OnSelectedColorModeOptionChanged(ScanColorModeOption? value)
        {
            foreach (var option in ColorModeOptions)
            {
                option.IsSelected = ReferenceEquals(option, value);
            }

            QueueSave(value != null);
        }
        partial void OnAutoCropChanged(bool value) => QueueSave();
        partial void OnAutoDeskewChanged(bool value) => QueueSave();
        partial void OnAutoRotateChanged(bool value) => QueueSave();
        partial void OnSaveErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasSaveError));

        private void QueueSave(bool isValidSelection = true)
        {
            if (!_isLoading && isValidSelection)
            {
                _ = SaveAsync();
            }
        }

        private void Reload()
        {
            _isLoading = true;
            try
            {
                var settings = _settingsService.ScanSettings;
                ResolutionOptions.Clear();
                ResolutionOptions.Add(new ScanResolutionOption(ScanResolution.Good, _localizationService.GetString("Settings.Scan.Resolution.Good"), SelectResolution));
                ResolutionOptions.Add(new ScanResolutionOption(ScanResolution.Better, _localizationService.GetString("Settings.Scan.Resolution.Better"), SelectResolution));
                ResolutionOptions.Add(new ScanResolutionOption(ScanResolution.Excellent, _localizationService.GetString("Settings.Scan.Resolution.Excellent"), SelectResolution));

                ColorModeOptions.Clear();
                ColorModeOptions.Add(new ScanColorModeOption(ScanColorMode.Color, _localizationService.GetString("Settings.Scan.ColorMode.Color"), SelectColorMode));
                ColorModeOptions.Add(new ScanColorModeOption(ScanColorMode.Grayscale, _localizationService.GetString("Settings.Scan.ColorMode.Grayscale"), SelectColorMode));
                ColorModeOptions.Add(new ScanColorModeOption(ScanColorMode.BlackAndWhite, _localizationService.GetString("Settings.Scan.ColorMode.BlackAndWhite"), SelectColorMode));

                SelectedResolutionOption = ResolutionOptions.First(option => option.Value == settings.Resolution);
                SelectedColorModeOption = ColorModeOptions.First(option => option.Value == settings.ColorMode);
                AutoCrop = settings.AutoCrop;
                AutoDeskew = settings.AutoDeskew;
                AutoRotate = settings.AutoRotate;
                SaveErrorMessage = string.Empty;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SelectResolution(ScanResolutionOption option)
        {
            if (ReferenceEquals(SelectedResolutionOption, option))
            {
                option.IsSelected = false;
                option.IsSelected = true;
                return;
            }

            SelectedResolutionOption = option;
        }

        private void SelectColorMode(ScanColorModeOption option)
        {
            if (ReferenceEquals(SelectedColorModeOption, option))
            {
                option.IsSelected = false;
                option.IsSelected = true;
                return;
            }

            SelectedColorModeOption = option;
        }

        private async Task SaveAsync()
        {
            if (SelectedResolutionOption == null || SelectedColorModeOption == null)
            {
                return;
            }

            var settings = new ScanSettings
            {
                Resolution = SelectedResolutionOption.Value,
                ColorMode = SelectedColorModeOption.Value,
                AutoCrop = AutoCrop,
                AutoDeskew = AutoDeskew,
                AutoRotate = AutoRotate
            };

            await _saveSemaphore.WaitAsync();
            try
            {
                await _settingsService.SetScanSettingsAsync(settings);
                SaveErrorMessage = string.Empty;
            }
            catch
            {
                SaveErrorMessage = _localizationService.GetString("Settings.Scan.SaveError");
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }
    }

    public sealed partial class ScanResolutionOption : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public ScanResolution Value { get; }
        public string Label { get; }
        public IRelayCommand SelectCommand { get; }

        public ScanResolutionOption(ScanResolution value, string label, Action<ScanResolutionOption> select)
        {
            Value = value;
            Label = label;
            SelectCommand = new RelayCommand(() => select(this));
        }
    }

    public sealed partial class ScanColorModeOption : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public ScanColorMode Value { get; }
        public string Label { get; }
        public IRelayCommand SelectCommand { get; }

        public ScanColorModeOption(ScanColorMode value, string label, Action<ScanColorModeOption> select)
        {
            Value = value;
            Label = label;
            SelectCommand = new RelayCommand(() => select(this));
        }
    }
}

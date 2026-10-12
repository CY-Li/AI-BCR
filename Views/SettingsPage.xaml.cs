using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;
using PlustekBCR.ViewModels;

namespace PlustekBCR.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILocalizationService _localizationService;
        public DuplicateSettingsViewModel DuplicateViewModel { get; }
        public ScanSettingsViewModel ScanViewModel { get; }
        public DefaultLinkAppsViewModel LinkAppsViewModel { get; }
        public string ApplicationVersion { get; } = GetApplicationVersion();
        public string FirmwareVersion { get; } = string.Empty;
        private readonly ObservableCollection<SelectionOption> _marketOptions = new();
        private readonly ObservableCollection<SelectionOption> _uiLanguageOptions = new();
        private bool _isSyncingSelection;
        private const string GeneralSection = "General";

        private static string GetApplicationVersion()
        {
            var version = typeof(App).Assembly.GetName().Version;
            return version == null
                ? string.Empty
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public SettingsPage()
        {
            _settingsService = App.GetService<IApplicationSettingsService>();
            _localizationService = App.GetService<ILocalizationService>();
            DuplicateViewModel = App.GetService<DuplicateSettingsViewModel>();
            ScanViewModel = App.GetService<ScanSettingsViewModel>();
            LinkAppsViewModel = App.GetService<DefaultLinkAppsViewModel>();
            InitializeComponent();
            DataContext = App.GetService<LocalizedStrings>();
            LanguageComboBox.ItemsSource = _marketOptions;
            UiLanguageComboBox.ItemsSource = _uiLanguageOptions;
            InitializeSelectionOptions();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            UpdateComboBoxItemText();
            SyncSelectionFromSettings();
            if (App.Window != null)
            {
                App.Window.Activated -= OnAppWindowActivated;
                App.Window.Activated += OnAppWindowActivated;
            }

            RefreshLinkAppsIfVisible();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var sectionName = e.Parameter as string;
            _currentSectionName = sectionName;
            ApplySectionLayout(sectionName);
        }

        private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSelection || LanguageComboBox.SelectedItem is not SelectionOption item || string.IsNullOrWhiteSpace(item.Tag))
            {
                return;
            }

            var market = item.Tag == "US" ? MarketCode.US : MarketCode.JP;
            await _settingsService.SetCurrentMarketAsync(market);
        }

        private async void OnUiLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSelection || UiLanguageComboBox.SelectedItem is not SelectionOption item || string.IsNullOrWhiteSpace(item.Tag))
            {
                return;
            }

            await _localizationService.SetLanguageAsync(item.Tag);
        }

        private void SyncSelectionFromSettings()
        {
            _isSyncingSelection = true;
            try
            {
                LanguageComboBox.SelectedValue = _settingsService.CurrentMarket == MarketCode.US ? "US" : "JP";
                UiLanguageComboBox.SelectedValue =
                    string.Equals(_localizationService.CurrentLanguageTag, LocalizationService.JapaneseLanguageTag, StringComparison.OrdinalIgnoreCase)
                        ? LocalizationService.JapaneseLanguageTag
                        : LocalizationService.EnglishLanguageTag;
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private void UpdateComboBoxItemText()
        {
            UpdateSelectionOptionLabel(_marketOptions, "JP", _localizationService.GetString("Settings.Market.Japanese"));
            UpdateSelectionOptionLabel(_marketOptions, "US", _localizationService.GetString("Settings.Market.English"));
            UpdateSelectionOptionLabel(_uiLanguageOptions, LocalizationService.EnglishLanguageTag, _localizationService.GetString("Settings.UiLanguage.English"));
            UpdateSelectionOptionLabel(_uiLanguageOptions, LocalizationService.JapaneseLanguageTag, _localizationService.GetString("Settings.UiLanguage.Japanese"));
        }

        private void InitializeSelectionOptions()
        {
            _marketOptions.Clear();
            _marketOptions.Add(new SelectionOption("JP", _localizationService.GetString("Settings.Market.Japanese")));
            _marketOptions.Add(new SelectionOption("US", _localizationService.GetString("Settings.Market.English")));

            _uiLanguageOptions.Clear();
            _uiLanguageOptions.Add(new SelectionOption(LocalizationService.EnglishLanguageTag, _localizationService.GetString("Settings.UiLanguage.English")));
            _uiLanguageOptions.Add(new SelectionOption(LocalizationService.JapaneseLanguageTag, _localizationService.GetString("Settings.UiLanguage.Japanese")));
        }

        private static void UpdateSelectionOptionLabel(ObservableCollection<SelectionOption> options, string tag, string label)
        {
            foreach (var item in options)
            {
                if (string.Equals(item.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    item.Label = label;
                    break;
                }
            }
        }

        private void OnBackToCardsClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.ReturnToCardsWorkspace();
            }
        }

        private void ApplySectionLayout(string? sectionName)
        {
            var normalizedSection = string.IsNullOrWhiteSpace(sectionName)
                ? GeneralSection
                : sectionName;

            var isGeneralSection = string.Equals(normalizedSection, GeneralSection, System.StringComparison.OrdinalIgnoreCase);
            var isScanSection = string.Equals(normalizedSection, "Scan", System.StringComparison.OrdinalIgnoreCase);
            var isDuplicateDetectionSection = string.Equals(normalizedSection, "DuplicateDetection", System.StringComparison.OrdinalIgnoreCase);
            var isLinkAppsSection = string.Equals(normalizedSection, "LinkApps", System.StringComparison.OrdinalIgnoreCase);
            var isAboutSection = string.Equals(normalizedSection, "About", System.StringComparison.OrdinalIgnoreCase);

            SectionTitleTextBlock.Text = normalizedSection switch
            {
                "Scan" => _localizationService.GetString("Main.Navigation.Scan"),
                "DuplicateDetection" => _localizationService.GetString("Main.Navigation.DuplicateDetection"),
                "Import" => _localizationService.GetString("Main.Navigation.Import"),
                "RecognitionAi" => _localizationService.GetString("Main.Navigation.RecognitionAi"),
                "Scanner" => _localizationService.GetString("Main.Navigation.Scanner"),
                "LinkApps" => _localizationService.GetString("Settings.LinkApps.Title"),
                "About" => _localizationService.GetString("Main.Navigation.About"),
                _ => _localizationService.GetString("Settings.General.Title")
            };

            SectionDescriptionTextBlock.Text = isScanSection
                ? _localizationService.GetString("Settings.Scan.Description")
                : isDuplicateDetectionSection
                    ? _localizationService.GetString("Settings.Duplicate.Description")
                    : isLinkAppsSection
                        ? _localizationService.GetString("Settings.LinkApps.Description")
                    : isAboutSection
                        ? _localizationService.GetString("Settings.About.Description")
                        : _localizationService.GetString("Settings.General.Description");
            SectionDescriptionTextBlock.Visibility = isGeneralSection || isScanSection || isDuplicateDetectionSection || isLinkAppsSection || isAboutSection ? Visibility.Visible : Visibility.Collapsed;
            GeneralContentPanel.Visibility = isGeneralSection || isDuplicateDetectionSection ? Visibility.Visible : Visibility.Collapsed;
            LanguageSettingsCard.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            DuplicateSettingsCard.Visibility = isDuplicateDetectionSection ? Visibility.Visible : Visibility.Collapsed;
            RegionSettingsCard.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            ScanContentPanel.Visibility = isScanSection ? Visibility.Visible : Visibility.Collapsed;
            LinkAppsContentPanel.Visibility = isLinkAppsSection ? Visibility.Visible : Visibility.Collapsed;
            AboutContentPanel.Visibility = isAboutSection ? Visibility.Visible : Visibility.Collapsed;
            SectionHintBorder.Visibility = isGeneralSection || isScanSection || isDuplicateDetectionSection || isLinkAppsSection || isAboutSection ? Visibility.Collapsed : Visibility.Visible;
            SectionHintTextBlock.Text = normalizedSection switch
            {
                "Import" => _localizationService.GetString("Settings.Import.Hint"),
                "RecognitionAi" => _localizationService.GetString("Settings.RecognitionAi.Hint"),
                "Scanner" => _localizationService.GetString("Settings.Scanner.Hint"),
                "About" => _localizationService.GetString("Settings.About.Hint"),
                _ => _localizationService.GetString("Settings.General.Hint")
            };

            RefreshLinkAppsIfVisible();
        }

        private void OnAppWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                RefreshLinkAppsIfVisible();
            }
        }

        private void RefreshLinkAppsIfVisible()
        {
            if (string.Equals(_currentSectionName, "LinkApps", StringComparison.OrdinalIgnoreCase))
            {
                LinkAppsViewModel.RefreshCommand.Execute(null);
            }
        }

        private async void OnChangePhoneAppClicked(object sender, RoutedEventArgs e) =>
            await ShowLinkAppChangeGuideAsync("Settings.LinkApps.Dialog.Phone.Title", new[] { "TEL" }, false);

        private async void OnChangeEmailAppClicked(object sender, RoutedEventArgs e) =>
            await ShowLinkAppChangeGuideAsync("Settings.LinkApps.Dialog.Email.Title", new[] { "MAILTO" }, false);

        private async void OnChangeWebAppClicked(object sender, RoutedEventArgs e) =>
            await ShowLinkAppChangeGuideAsync("Settings.LinkApps.Dialog.Web.Title", new[] { "HTTP", "HTTPS" }, true);

        private async Task ShowLinkAppChangeGuideAsync(string titleKey, IReadOnlyList<string> protocols, bool useSameApp)
        {
            try
            {
                var dialog = DialogHelper.CreateDialog(
                    XamlRoot,
                    _localizationService.GetString(titleKey),
                    CreateLinkAppGuideContent(protocols, useSameApp),
                    primaryButtonText: _localizationService.GetString("Settings.LinkApps.Dialog.OpenSettings"),
                    closeButtonText: _localizationService.GetString("Button.Cancel"));

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await LinkAppsViewModel.OpenWindowsSettingsCommand.ExecuteAsync(null);
                }
            }
            catch
            {
                LinkAppsViewModel.ErrorMessage = _localizationService.GetString("Settings.LinkApps.OpenFailed");
            }
        }

        private StackPanel CreateLinkAppGuideContent(IReadOnlyList<string> protocols, bool useSameApp)
        {
            var content = new StackPanel
            {
                Width = 440,
                Spacing = 18
            };

            content.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Settings.LinkApps.Dialog.Intro"),
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            content.Children.Add(CreateGuideStep("1", _localizationService.GetString("Settings.LinkApps.Dialog.Step.Open")));

            var findStep = CreateGuideStep("2", _localizationService.GetString("Settings.LinkApps.Dialog.Step.Search"));
            var protocolPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(40, -8, 0, 0)
            };
            foreach (var protocol in protocols)
            {
                protocolPanel.Children.Add(CreateProtocolBadge(protocol));
            }

            content.Children.Add(findStep);
            content.Children.Add(protocolPanel);
            content.Children.Add(CreateGuideStep(
                "3",
                _localizationService.GetString(useSameApp
                    ? "Settings.LinkApps.Dialog.Step.SelectWeb"
                    : "Settings.LinkApps.Dialog.Step.Select")));
            content.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Settings.LinkApps.Dialog.ReturnHint"),
                Margin = new Thickness(40, 0, 0, 0),
                FontSize = 12,
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            return content;
        }

        private static Grid CreateGuideStep(string number, string text)
        {
            var step = new Grid { ColumnSpacing = 12 };
            step.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            step.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var numberBadge = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = new TextBlock
                {
                    Text = number,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var description = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
            };

            step.Children.Add(numberBadge);
            Grid.SetColumn(description, 1);
            step.Children.Add(description);
            return step;
        }

        private static Border CreateProtocolBadge(string protocol) => new()
        {
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(5),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            Child = new TextBlock
            {
                Text = protocol,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            }
        };

        private void OnLanguageChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateComboBoxItemText();
                SyncSelectionFromSettings();
                ApplySectionLayout(_currentSectionName);
            });
        }

        private string? _currentSectionName;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (App.Window != null)
            {
                App.Window.Activated -= OnAppWindowActivated;
            }

            _localizationService.LanguageChanged -= OnLanguageChanged;
            Unloaded -= OnUnloaded;
        }

        private sealed class SelectionOption : INotifyPropertyChanged
        {
            private string _label;

            public SelectionOption(string tag, string label)
            {
                Tag = tag;
                _label = label;
            }

            public string Tag { get; }

            public string Label
            {
                get => _label;
                set
                {
                    if (string.Equals(_label, value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _label = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}

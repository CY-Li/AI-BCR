using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;

namespace PlustekBCR.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILocalizationService _localizationService;
        private readonly ObservableCollection<SelectionOption> _marketOptions = new();
        private readonly ObservableCollection<SelectionOption> _uiLanguageOptions = new();
        private bool _isSyncingSelection;
        private const string GeneralSection = "General";

        public SettingsPage()
        {
            _settingsService = App.GetService<IApplicationSettingsService>();
            _localizationService = App.GetService<ILocalizationService>();
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

            SectionTitleTextBlock.Text = normalizedSection switch
            {
                "Import" => _localizationService.GetString("Main.Navigation.Import"),
                "RecognitionAi" => _localizationService.GetString("Main.Navigation.RecognitionAi"),
                "Scanner" => _localizationService.GetString("Main.Navigation.Scanner"),
                "About" => _localizationService.GetString("Main.Navigation.About"),
                _ => _localizationService.GetString("Settings.General.Title")
            };

            SectionDescriptionTextBlock.Text = _localizationService.GetString("Settings.General.Description");
            SectionDescriptionTextBlock.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            GeneralContentPanel.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            SectionHintBorder.Visibility = isGeneralSection ? Visibility.Collapsed : Visibility.Visible;
            SectionHintTextBlock.Text = normalizedSection switch
            {
                "Import" => _localizationService.GetString("Settings.Import.Hint"),
                "RecognitionAi" => _localizationService.GetString("Settings.RecognitionAi.Hint"),
                "Scanner" => _localizationService.GetString("Settings.Scanner.Hint"),
                "About" => _localizationService.GetString("Settings.About.Hint"),
                _ => _localizationService.GetString("Settings.General.Hint")
            };
        }

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

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly IApplicationSettingsService _settingsService;
        private bool _isSyncingSelection;
        private const string GeneralSection = "General";

        public SettingsPage()
        {
            _settingsService = App.GetService<IApplicationSettingsService>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SyncSelectionFromSettings();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var sectionName = e.Parameter as string;
            ApplySectionLayout(sectionName);
        }

        private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSelection || LanguageComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            {
                return;
            }

            var market = tag == "US" ? MarketCode.US : MarketCode.JP;
            await _settingsService.SetCurrentMarketAsync(market);
        }

        private void SyncSelectionFromSettings()
        {
            _isSyncingSelection = true;
            try
            {
                LanguageComboBox.SelectedIndex = _settingsService.CurrentMarket == MarketCode.US ? 1 : 0;
            }
            finally
            {
                _isSyncingSelection = false;
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
                "Import" => "Import",
                "RecognitionAi" => "Recognition / AI",
                "Scanner" => "Scanner",
                "About" => "About",
                _ => "General"
            };

            SectionDescriptionTextBlock.Text = "Apply language and field presentation defaults for operators working in this environment.";
            SectionDescriptionTextBlock.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            GeneralContentPanel.Visibility = isGeneralSection ? Visibility.Visible : Visibility.Collapsed;
            SectionHintBorder.Visibility = isGeneralSection ? Visibility.Collapsed : Visibility.Visible;
            SectionHintTextBlock.Text = normalizedSection switch
            {
                "Import" => "Import settings will be added in the next prototype iteration.",
                "RecognitionAi" => "Recognition / AI settings will be added in the next prototype iteration.",
                "Scanner" => "Scanner settings will be added in the next prototype iteration.",
                "About" => "About information will be added in the next prototype iteration.",
                _ => "General settings are shown below."
            };
        }
    }
}

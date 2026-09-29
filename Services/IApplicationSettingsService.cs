using System;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IApplicationSettingsService
    {
        MarketCode CurrentMarket { get; }
        event Action<MarketCode>? CurrentMarketChanged;
        string CurrentUiLanguage { get; }
        event Action<string>? CurrentUiLanguageChanged;
        bool IsAiEnabled { get; }
        event Action<bool>? AiEnabledChanged;
        CardViewMode CardViewMode { get; }
        event Action<CardViewMode>? CardViewModeChanged;
        DuplicateComparisonSettings DuplicateComparison { get; }
        event Action<DuplicateComparisonSettings>? DuplicateComparisonChanged;
        ScanSettings ScanSettings { get; }
        event Action<ScanSettings>? ScanSettingsChanged;
        Task SetCurrentMarketAsync(MarketCode market);
        Task SetCurrentUiLanguageAsync(string languageTag);
        Task SetAiEnabledAsync(bool isEnabled);
        Task SetCardViewModeAsync(CardViewMode viewMode);
        Task SetDuplicateComparisonAsync(DuplicateComparisonSettings settings);
        Task SetScanSettingsAsync(ScanSettings settings);
    }
}

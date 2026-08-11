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
        DuplicateComparisonSettings DuplicateComparison { get; }
        event Action<DuplicateComparisonSettings>? DuplicateComparisonChanged;
        Task SetCurrentMarketAsync(MarketCode market);
        Task SetCurrentUiLanguageAsync(string languageTag);
        Task SetAiEnabledAsync(bool isEnabled);
        Task SetDuplicateComparisonAsync(DuplicateComparisonSettings settings);
    }
}

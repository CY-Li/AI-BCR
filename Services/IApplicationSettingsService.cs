using System;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IApplicationSettingsService
    {
        MarketCode CurrentMarket { get; }
        event Action<MarketCode>? CurrentMarketChanged;
        bool IsAiEnabled { get; }
        event Action<bool>? AiEnabledChanged;
        Task SetCurrentMarketAsync(MarketCode market);
        Task SetAiEnabledAsync(bool isEnabled);
    }
}

using Microsoft.Extensions.Configuration;
using PlustekBCR.Models;
using PlustekBCR.Models.Plustek;

namespace PlustekBCR.Services.Plustek
{
    public class PlustekOptionsProvider : IPlustekOptionsProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IApplicationSettingsService _applicationSettingsService;

        public PlustekOptionsProvider(
            IConfiguration configuration,
            IApplicationSettingsService applicationSettingsService)
        {
            _configuration = configuration;
            _applicationSettingsService = applicationSettingsService;
        }

        public PlustekConsoleOptions GetCurrent()
        {
            return Get(_applicationSettingsService.CurrentMarket);
        }

        public PlustekConsoleOptions Get(MarketCode market)
        {
            var marketSection = _configuration.GetSection($"PlustekConsole:{market}");
            if (marketSection.Exists())
            {
                return new PlustekConsoleOptions
                {
                    BaseUrl = marketSection["BaseUrl"] ?? string.Empty,
                    ApiKey = marketSection["ApiKey"] ?? string.Empty,
                    ApiSecret = marketSection["ApiSecret"] ?? string.Empty,
                    EscanServiceId = marketSection["EscanServiceId"] ?? string.Empty,
                    PollIntervalMs = ParseInt(marketSection["PollIntervalMs"], 1500),
                    ResultTimeoutSeconds = ParseInt(marketSection["ResultTimeoutSeconds"], 45)
                };
            }

            // Backward-compatible fallback for older single-market appsettings schema.
            var legacySection = _configuration.GetSection("PlustekConsole");
            return new PlustekConsoleOptions
            {
                BaseUrl = legacySection["BaseUrl"] ?? string.Empty,
                ApiKey = legacySection["ApiKey"] ?? string.Empty,
                ApiSecret = legacySection["ApiSecret"] ?? string.Empty,
                EscanServiceId = legacySection["EscanServiceId"] ?? string.Empty,
                PollIntervalMs = ParseInt(legacySection["PollIntervalMs"], 1500),
                ResultTimeoutSeconds = ParseInt(legacySection["ResultTimeoutSeconds"], 45)
            };
        }

        private static int ParseInt(string? value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}

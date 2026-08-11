#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public class ApplicationSettingsService : IApplicationSettingsService
    {
        private readonly string _settingsPath;
        private MarketCode _currentMarket;
        private string _currentUiLanguage;
        private bool _isAiEnabled;
        private DuplicateComparisonSettings _duplicateComparison;

        public event Action<MarketCode>? CurrentMarketChanged;
        public event Action<string>? CurrentUiLanguageChanged;
        public event Action<bool>? AiEnabledChanged;
        public event Action<DuplicateComparisonSettings>? DuplicateComparisonChanged;

        public MarketCode CurrentMarket => _currentMarket;
        public string CurrentUiLanguage => _currentUiLanguage;
        public bool IsAiEnabled => _isAiEnabled;
        public DuplicateComparisonSettings DuplicateComparison => _duplicateComparison.Clone();

        public ApplicationSettingsService()
        {
            _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _currentMarket = LoadCurrentMarket();
            _currentUiLanguage = LoadCurrentUiLanguage();
            _isAiEnabled = LoadAiEnabled();
            _duplicateComparison = LoadDuplicateComparison();
        }

        public async Task SetCurrentMarketAsync(MarketCode market)
        {
            if (_currentMarket == market)
            {
                return;
            }

            _currentMarket = market;
            await SaveCurrentMarketAsync(market);
            CurrentMarketChanged?.Invoke(_currentMarket);
        }

        public async Task SetCurrentUiLanguageAsync(string languageTag)
        {
            var normalized = NormalizeLanguageTag(languageTag);
            if (string.Equals(_currentUiLanguage, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentUiLanguage = normalized;
            await SaveCurrentUiLanguageAsync(normalized);
            CurrentUiLanguageChanged?.Invoke(_currentUiLanguage);
        }

        public async Task SetAiEnabledAsync(bool isEnabled)
        {
            if (_isAiEnabled == isEnabled)
            {
                return;
            }

            _isAiEnabled = isEnabled;
            await SaveAiEnabledAsync(isEnabled);
            AiEnabledChanged?.Invoke(_isAiEnabled);
        }

        public async Task SetDuplicateComparisonAsync(DuplicateComparisonSettings settings)
        {
            var normalized = NormalizeDuplicateComparison(settings);
            if (_duplicateComparison.MatchOperator == normalized.MatchOperator
                && _duplicateComparison.Fields.SequenceEqual(normalized.Fields, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            await SaveDuplicateComparisonAsync(normalized);
            _duplicateComparison = normalized;
            DuplicateComparisonChanged?.Invoke(_duplicateComparison.Clone());
        }

        private MarketCode LoadCurrentMarket()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return MarketCode.JP;
                }

                var text = File.ReadAllText(_settingsPath);
                var root = JsonNode.Parse(text) as JsonObject;
                var configured = root?["BusinessCard"]?["CurrentMarket"]?.ToString();
                return ParseMarket(configured);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load current market failed: {ex.Message}");
                return MarketCode.JP;
            }
        }

        private string LoadCurrentUiLanguage()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return string.Empty;
                }

                var text = File.ReadAllText(_settingsPath);
                var root = JsonNode.Parse(text) as JsonObject;
                return NormalizeLanguageTag(root?["Localization"]?["UiLanguage"]?.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load current UI language failed: {ex.Message}");
                return string.Empty;
            }
        }

        private bool LoadAiEnabled()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return true;
                }

                var text = File.ReadAllText(_settingsPath);
                var root = JsonNode.Parse(text) as JsonObject;
                var configured = root?["Recognition"]?["IsAiEnabled"]?.GetValue<bool?>();
                return configured ?? true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load AI enabled failed: {ex.Message}");
                return true;
            }
        }

        private DuplicateComparisonSettings LoadDuplicateComparison()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return DuplicateComparisonSettings.Default;
                }

                var root = JsonNode.Parse(File.ReadAllText(_settingsPath)) as JsonObject;
                var section = root?["DuplicateDetection"] as JsonObject;
                var configuredOperator = section?["MatchOperator"]?.ToString();
                var fields = section?["Fields"] is JsonArray fieldArray
                    ? fieldArray.Select(value => value?.ToString() ?? string.Empty).ToList()
                    : new List<string>();

                return NormalizeDuplicateComparison(new DuplicateComparisonSettings
                {
                    MatchOperator = Enum.TryParse<DuplicateMatchOperator>(configuredOperator, true, out var parsed)
                        ? parsed
                        : DuplicateMatchOperator.Or,
                    Fields = fields
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load duplicate comparison failed: {ex.Message}");
                return DuplicateComparisonSettings.Default;
            }
        }

        private async Task SaveCurrentMarketAsync(MarketCode market)
        {
            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                {
                    var text = await File.ReadAllTextAsync(_settingsPath);
                    root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                var businessCard = root["BusinessCard"] as JsonObject ?? new JsonObject();
                businessCard["CurrentMarket"] = market.ToString();
                root["BusinessCard"] = businessCard;

                var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save current market failed: {ex.Message}");
            }
        }

        private async Task SaveCurrentUiLanguageAsync(string languageTag)
        {
            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                {
                    var text = await File.ReadAllTextAsync(_settingsPath);
                    root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                var localization = root["Localization"] as JsonObject ?? new JsonObject();
                localization["UiLanguage"] = languageTag;
                root["Localization"] = localization;

                var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save current UI language failed: {ex.Message}");
            }
        }

        private async Task SaveAiEnabledAsync(bool isEnabled)
        {
            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                {
                    var text = await File.ReadAllTextAsync(_settingsPath);
                    root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                var recognition = root["Recognition"] as JsonObject ?? new JsonObject();
                recognition["IsAiEnabled"] = isEnabled;
                root["Recognition"] = recognition;

                var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save AI enabled failed: {ex.Message}");
            }
        }

        private async Task SaveDuplicateComparisonAsync(DuplicateComparisonSettings settings)
        {
            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                {
                    root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath)) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                root["DuplicateDetection"] = new JsonObject
                {
                    ["MatchOperator"] = settings.MatchOperator.ToString(),
                    ["Fields"] = new JsonArray(settings.Fields
                        .Select(field => (JsonNode?)JsonValue.Create(field))
                        .ToArray())
                };

                await File.WriteAllTextAsync(
                    _settingsPath,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save duplicate comparison failed: {ex.Message}");
                throw;
            }
        }

        private static DuplicateComparisonSettings NormalizeDuplicateComparison(DuplicateComparisonSettings? settings)
        {
            var fields = settings?.Fields?
                .Where(field => !string.IsNullOrWhiteSpace(field)
                    && DuplicateComparisonSettings.SupportedFieldKeys.Contains(field))
                .Select(field => field.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (fields.Count == 0)
            {
                fields.Add("email");
            }

            return new DuplicateComparisonSettings
            {
                MatchOperator = settings?.MatchOperator ?? DuplicateMatchOperator.Or,
                Fields = fields
            };
        }

        private static MarketCode ParseMarket(string? configured)
        {
            return Enum.TryParse(configured, true, out MarketCode market) ? market : MarketCode.JP;
        }

        private static string NormalizeLanguageTag(string? configured)
        {
            return configured?.Trim() ?? string.Empty;
        }
    }
}

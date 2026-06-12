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
        private bool _isAiEnabled;

        public event Action<MarketCode>? CurrentMarketChanged;
        public event Action<bool>? AiEnabledChanged;

        public MarketCode CurrentMarket => _currentMarket;
        public bool IsAiEnabled => _isAiEnabled;

        public ApplicationSettingsService()
        {
            _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _currentMarket = LoadCurrentMarket();
            _isAiEnabled = LoadAiEnabled();
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

        private static MarketCode ParseMarket(string? configured)
        {
            return Enum.TryParse(configured, true, out MarketCode market) ? market : MarketCode.JP;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public sealed class LocalizationService : ILocalizationService
    {
        public const string EnglishLanguageTag = "en-US";
        public const string JapaneseLanguageTag = "ja-JP";

        private static readonly string[] SupportedLanguages = { EnglishLanguageTag, JapaneseLanguageTag };
        private static readonly ResourceManager ResourceManager = new("PlustekBCR.Resources.Strings", Assembly.GetExecutingAssembly());
        private readonly IApplicationSettingsService _settingsService;
        private readonly CultureInfo _fallbackCulture = CultureInfo.GetCultureInfo(EnglishLanguageTag);
        private CultureInfo _currentCulture;
        private string _currentLanguageTag;

        public LocalizationService(IApplicationSettingsService settingsService)
        {
            _settingsService = settingsService;
            _currentLanguageTag = NormalizeLanguageTag(settingsService.CurrentUiLanguage, GetDefaultLanguageTag());
            _currentCulture = CultureInfo.GetCultureInfo(_currentLanguageTag);
            ApplyCulture(_currentCulture);
        }

        public string CurrentLanguageTag => _currentLanguageTag;

        public CultureInfo CurrentCulture => _currentCulture;

        public IReadOnlyList<string> SupportedLanguageTags => SupportedLanguages;

        public event Action? LanguageChanged;

        public string GetString(string key)
        {
            var localized = ResourceManager.GetString(key, _currentCulture);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }

            var fallback = ResourceManager.GetString(key, _fallbackCulture);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                Debug.WriteLine($"Localization fallback used for key '{key}' in culture '{_currentLanguageTag}'.");
                return fallback;
            }

            Debug.WriteLine($"Missing localization key '{key}'.");
            return key;
        }

        public string Format(string key, params object[] args)
        {
            var template = GetString(key);
            return string.Format(_currentCulture, template, args);
        }

        public async Task SetLanguageAsync(string languageTag)
        {
            var normalized = NormalizeLanguageTag(languageTag, GetDefaultLanguageTag());
            if (string.Equals(_currentLanguageTag, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentLanguageTag = normalized;
            _currentCulture = CultureInfo.GetCultureInfo(normalized);
            ApplyCulture(_currentCulture);
            await _settingsService.SetCurrentUiLanguageAsync(normalized);
            LanguageChanged?.Invoke();
        }

        private static string GetDefaultLanguageTag()
        {
            var currentUi = CultureInfo.InstalledUICulture.Name;
            return currentUi.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
                ? JapaneseLanguageTag
                : EnglishLanguageTag;
        }

        private static string NormalizeLanguageTag(string? configuredLanguageTag, string fallbackLanguageTag)
        {
            if (string.IsNullOrWhiteSpace(configuredLanguageTag))
            {
                return fallbackLanguageTag;
            }

            foreach (var supported in SupportedLanguages)
            {
                if (string.Equals(supported, configuredLanguageTag, StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }

            return fallbackLanguageTag;
        }

        private static void ApplyCulture(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public interface ILocalizationService
    {
        string CurrentLanguageTag { get; }
        CultureInfo CurrentCulture { get; }
        IReadOnlyList<string> SupportedLanguageTags { get; }
        event Action? LanguageChanged;
        string GetString(string key);
        string Format(string key, params object[] args);
        Task SetLanguageAsync(string languageTag);
    }
}

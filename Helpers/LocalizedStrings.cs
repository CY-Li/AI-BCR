using System.ComponentModel;
using PlustekBCR.Services;

namespace PlustekBCR.Helpers
{
    public sealed class LocalizedStrings : INotifyPropertyChanged
    {
        private readonly ILocalizationService _localizationService;

        public LocalizedStrings()
        {
            _localizationService = App.GetService<ILocalizationService>();
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        public string this[string key] => _localizationService.GetString(key);

        public string GetString(string key) => _localizationService.GetString(key);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnLanguageChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using PlustekBCR.Services;

namespace PlustekBCR.Models
{
    public partial class SearchScopeOption : ObservableObject
    {
        private readonly ILocalizationService _localizationService;

        public SearchScopeOption(string id, string labelKey)
        {
            Id = id;
            LabelKey = labelKey;
            _localizationService = App.GetService<ILocalizationService>();
        }

        public string Id { get; }

        public string LabelKey { get; }

        public string Label => _localizationService.GetString(LabelKey);

        public void RefreshLabel()
        {
            OnPropertyChanged(nameof(Label));
        }
    }
}

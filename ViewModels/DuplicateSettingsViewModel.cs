using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels
{
    public enum DuplicateSettingsSaveState
    {
        Idle,
        Saving,
        Saved,
        Failed
    }

    public sealed partial class DuplicateSettingsViewModel : ObservableObject
    {
        private static readonly string[] IdentityKeys = { "full_name", "company_name", "job_title" };
        private static readonly string[] ContactKeys = { "email", "tel", "mobile", "fax", "website" };
        private static readonly string[] AddressKeys = { "full_address", "zip_code", "country", "state", "city" };

        private readonly IApplicationSettingsService _settingsService;
        private readonly IBusinessCardFieldService _fieldService;
        private readonly ILocalizationService _localizationService;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private bool _isLoading;
        private bool _isCustomPreset;

        [ObservableProperty]
        public partial DuplicateOperatorOption? SelectedOperatorOption { get; set; }

        [ObservableProperty]
        public partial string RuleSummary { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ValidationMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SaveErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial DuplicateSettingsSaveState SaveState { get; set; }

        public ObservableCollection<DuplicateFieldOption> Fields { get; } = new();
        public ObservableCollection<DuplicateFieldGroup> FieldGroups { get; } = new();
        public ObservableCollection<DuplicatePresetOption> Presets { get; } = new();
        public ObservableCollection<DuplicateOperatorOption> Operators { get; } = new();
        public DuplicateFieldGroup? AdvancedFieldGroup { get; private set; }
        public IReadOnlyList<DuplicateFieldOption> AdvancedFields => AdvancedFieldGroup?.Fields ?? Array.Empty<DuplicateFieldOption>();
        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
        public bool HasSaveError => !string.IsNullOrWhiteSpace(SaveErrorMessage);
        public int SelectedFieldCount => Fields.Count(option => option.IsSelected);
        public string SelectedFieldCountText => _localizationService.Format("Settings.Duplicate.SelectedCount", SelectedFieldCount);
        public string CurrentPresetText => Presets.FirstOrDefault(preset => preset.IsActive)?.Label
            ?? _localizationService.GetString("Settings.Duplicate.Preset.Custom");
        public string SaveStatusText => SaveState switch
        {
            DuplicateSettingsSaveState.Saving => _localizationService.GetString("Settings.Duplicate.Status.Saving"),
            DuplicateSettingsSaveState.Saved => _localizationService.GetString("Settings.Duplicate.Status.Saved"),
            DuplicateSettingsSaveState.Failed => _localizationService.GetString("Settings.Duplicate.Status.Failed"),
            _ => string.Empty
        };
        public bool IsSaveFailed => SaveState == DuplicateSettingsSaveState.Failed;

        public DuplicateSettingsViewModel(
            IApplicationSettingsService settingsService,
            IBusinessCardFieldService fieldService,
            ILocalizationService localizationService)
        {
            _settingsService = settingsService;
            _fieldService = fieldService;
            _localizationService = localizationService;
            _localizationService.LanguageChanged += Reload;
            _settingsService.CurrentMarketChanged += _ => Reload();
            Reload();
        }

        partial void OnSelectedOperatorOptionChanged(DuplicateOperatorOption? value)
        {
            if (_isLoading || value == null)
            {
                return;
            }

            _isCustomPreset = true;
            UpdateDerivedState();
            _ = SaveAsync();
        }

        partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));
        partial void OnSaveErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasSaveError));
        partial void OnSaveStateChanged(DuplicateSettingsSaveState value)
        {
            OnPropertyChanged(nameof(SaveStatusText));
            OnPropertyChanged(nameof(IsSaveFailed));
        }

        private void Reload()
        {
            _isLoading = true;
            try
            {
                UnsubscribeFields();
                Fields.Clear();
                FieldGroups.Clear();
                Presets.Clear();
                Operators.Clear();

                var settings = _settingsService.DuplicateComparison;
                foreach (var definition in _fieldService.GetDuplicateComparisonFields())
                {
                    var option = new DuplicateFieldOption(
                        definition.Key,
                        _fieldService.GetLabel(definition.Key),
                        settings.Fields.Contains(definition.Key, StringComparer.OrdinalIgnoreCase));
                    option.PropertyChanged += OnFieldPropertyChanged;
                    Fields.Add(option);
                }

                BuildFieldGroups();
                BuildOperators(settings.MatchOperator);
                BuildPresets();
                ValidationMessage = string.Empty;
                SaveErrorMessage = string.Empty;
                SaveState = DuplicateSettingsSaveState.Idle;
                _isCustomPreset = false;
                UpdateDerivedState();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void BuildFieldGroups()
        {
            FieldGroups.Add(CreateGroup("Settings.Duplicate.Group.Identity", IdentityKeys));
            FieldGroups.Add(CreateGroup("Settings.Duplicate.Group.Contact", ContactKeys));
            FieldGroups.Add(CreateGroup("Settings.Duplicate.Group.Address", AddressKeys));

            var assignedKeys = IdentityKeys.Concat(ContactKeys).Concat(AddressKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
            AdvancedFieldGroup = new DuplicateFieldGroup(
                _localizationService.GetString("Settings.Duplicate.Group.Advanced"),
                Fields.Where(field => !assignedKeys.Contains(field.Key)).ToList());
            OnPropertyChanged(nameof(AdvancedFieldGroup));
            OnPropertyChanged(nameof(AdvancedFields));
        }

        private DuplicateFieldGroup CreateGroup(string labelKey, IEnumerable<string> keys)
        {
            var keySet = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new DuplicateFieldGroup(
                _localizationService.GetString(labelKey),
                Fields.Where(field => keySet.Contains(field.Key)).ToList());
        }

        private void BuildOperators(DuplicateMatchOperator selectedOperator)
        {
            var orOption = new DuplicateOperatorOption(
                DuplicateMatchOperator.Or,
                _localizationService.GetString("Settings.Duplicate.Operator.Or.Title"),
                _localizationService.GetString("Settings.Duplicate.Operator.Or.Description"),
                SelectOperator);
            var andOption = new DuplicateOperatorOption(
                DuplicateMatchOperator.And,
                _localizationService.GetString("Settings.Duplicate.Operator.And.Title"),
                _localizationService.GetString("Settings.Duplicate.Operator.And.Description"),
                SelectOperator);
            Operators.Add(orOption);
            Operators.Add(andOption);
            SelectedOperatorOption = selectedOperator == DuplicateMatchOperator.And ? andOption : orOption;
        }

        private void BuildPresets()
        {
            Presets.Add(new DuplicatePresetOption(
                "email",
                _localizationService.GetString("Settings.Duplicate.Preset.Email.Title"),
                _localizationService.GetString("Settings.Duplicate.Preset.Email.Description"),
                new[] { "email" },
                DuplicateMatchOperator.Or,
                ApplyPresetAsync));
            Presets.Add(new DuplicatePresetOption(
                "name_company",
                _localizationService.GetString("Settings.Duplicate.Preset.NameCompany.Title"),
                _localizationService.GetString("Settings.Duplicate.Preset.NameCompany.Description"),
                new[] { "full_name", "company_name" },
                DuplicateMatchOperator.And,
                ApplyPresetAsync));
            Presets.Add(new DuplicatePresetOption(
                "contact",
                _localizationService.GetString("Settings.Duplicate.Preset.Contact.Title"),
                _localizationService.GetString("Settings.Duplicate.Preset.Contact.Description"),
                new[] { "email", "tel", "mobile" },
                DuplicateMatchOperator.Or,
                ApplyPresetAsync));
            Presets.Add(new DuplicatePresetOption(
                "custom",
                _localizationService.GetString("Settings.Duplicate.Preset.Custom"),
                _localizationService.GetString("Settings.Duplicate.Preset.Custom.Description"),
                Array.Empty<string>(),
                DuplicateMatchOperator.Or,
                ApplyPresetAsync,
                isCustom: true));
        }

        private void SelectOperator(DuplicateOperatorOption option)
        {
            if (!ReferenceEquals(SelectedOperatorOption, option))
            {
                SelectedOperatorOption = option;
            }
        }

        private async Task ApplyPresetAsync(DuplicatePresetOption preset)
        {
            if (preset.IsCustom)
            {
                _isCustomPreset = true;
                UpdateDerivedState();
                return;
            }

            _isLoading = true;
            try
            {
                _isCustomPreset = false;
                var presetFields = preset.Fields.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var field in Fields)
                {
                    field.IsSelected = presetFields.Contains(field.Key);
                }

                SelectedOperatorOption = Operators.First(option => option.Value == preset.MatchOperator);
                ValidationMessage = string.Empty;
                SaveErrorMessage = string.Empty;
                UpdateDerivedState();
            }
            finally
            {
                _isLoading = false;
            }

            await SaveAsync();
        }

        private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || e.PropertyName != nameof(DuplicateFieldOption.IsSelected))
            {
                return;
            }

            if (SelectedFieldCount == 0 && sender is DuplicateFieldOption lastField)
            {
                _isLoading = true;
                lastField.IsSelected = true;
                _isLoading = false;
                ValidationMessage = _localizationService.GetString("Settings.Duplicate.Validation.FieldRequired");
                UpdateDerivedState();
                return;
            }

            ValidationMessage = string.Empty;
            _isCustomPreset = true;
            UpdateDerivedState();
            _ = SaveAsync();
        }

        private async Task SaveAsync()
        {
            await _saveSemaphore.WaitAsync();
            try
            {
                var settings = CreateCurrentSettings();
                SaveState = DuplicateSettingsSaveState.Saving;
                try
                {
                    await _settingsService.SetDuplicateComparisonAsync(settings);
                    SaveErrorMessage = string.Empty;
                    SaveState = DuplicateSettingsSaveState.Saved;
                    UpdateDerivedState();
                }
                catch
                {
                    var errorMessage = _localizationService.GetString("Settings.Duplicate.SaveError");
                    Reload();
                    SaveErrorMessage = errorMessage;
                    SaveState = DuplicateSettingsSaveState.Failed;
                }
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        private DuplicateComparisonSettings CreateCurrentSettings() => new()
        {
            MatchOperator = SelectedOperatorOption?.Value ?? DuplicateMatchOperator.Or,
            Fields = Fields.Where(field => field.IsSelected).Select(field => field.Key).ToList()
        };

        private void UpdateDerivedState()
        {
            var settings = CreateCurrentSettings();
            foreach (var operatorOption in Operators)
            {
                operatorOption.IsActive = operatorOption.Value == settings.MatchOperator;
            }

            var standardPresetMatched = false;
            foreach (var preset in Presets.Where(option => !option.IsCustom))
            {
                preset.IsActive = !_isCustomPreset && preset.Matches(settings);
                standardPresetMatched |= preset.IsActive;
            }

            foreach (var preset in Presets.Where(option => option.IsCustom))
            {
                preset.IsActive = _isCustomPreset || !standardPresetMatched;
            }

            var labels = Fields.Where(field => field.IsSelected).Select(field => field.Label);
            var separator = settings.MatchOperator == DuplicateMatchOperator.And
                ? _localizationService.GetString("Settings.Duplicate.Summary.AndSeparator")
                : _localizationService.GetString("Settings.Duplicate.Summary.OrSeparator");
            RuleSummary = _localizationService.Format("Settings.Duplicate.Summary", string.Join(separator, labels));
            OnPropertyChanged(nameof(SelectedFieldCount));
            OnPropertyChanged(nameof(SelectedFieldCountText));
            OnPropertyChanged(nameof(CurrentPresetText));
        }

        private void UnsubscribeFields()
        {
            foreach (var field in Fields)
            {
                field.PropertyChanged -= OnFieldPropertyChanged;
            }
        }
    }

    public sealed partial class DuplicateFieldOption : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public DuplicateFieldOption(string key, string label, bool isSelected)
        {
            Key = key;
            Label = label;
            IsSelected = isSelected;
        }

        public string Key { get; }
        public string Label { get; }
    }

    public sealed class DuplicateFieldGroup
    {
        public DuplicateFieldGroup(string label, IReadOnlyList<DuplicateFieldOption> fields)
        {
            Label = label;
            Fields = fields;
        }

        public string Label { get; }
        public IReadOnlyList<DuplicateFieldOption> Fields { get; }
    }

    public sealed partial class DuplicatePresetOption : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsActive { get; set; }

        public DuplicatePresetOption(
            string key,
            string label,
            string description,
            IReadOnlyList<string> fields,
            DuplicateMatchOperator matchOperator,
            Func<DuplicatePresetOption, Task> applyAsync,
            bool isCustom = false)
        {
            Key = key;
            Label = label;
            Description = description;
            Fields = fields;
            MatchOperator = matchOperator;
            IsCustom = isCustom;
            ApplyCommand = new AsyncRelayCommand(() => applyAsync(this));
        }

        public string Key { get; }
        public string Label { get; }
        public string Description { get; }
        public IReadOnlyList<string> Fields { get; }
        public DuplicateMatchOperator MatchOperator { get; }
        public bool IsCustom { get; }
        public IAsyncRelayCommand ApplyCommand { get; }

        public bool Matches(DuplicateComparisonSettings settings)
        {
            return settings.MatchOperator == MatchOperator
                && Fields.Count == settings.Fields.Count
                && Fields.All(field => settings.Fields.Contains(field, StringComparer.OrdinalIgnoreCase));
        }
    }

    public sealed partial class DuplicateOperatorOption : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsActive { get; set; }

        public DuplicateOperatorOption(
            DuplicateMatchOperator value,
            string label,
            string description,
            Action<DuplicateOperatorOption> select)
        {
            Value = value;
            Label = label;
            Description = description;
            SelectCommand = new RelayCommand(() => select(this));
        }

        public DuplicateMatchOperator Value { get; }
        public string Label { get; }
        public string Description { get; }
        public IRelayCommand SelectCommand { get; }
    }
}

#nullable enable
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels
{
    public enum AutoScanState
    {
        Idle,
        Connecting,
        Ready,
        Scanning,
        Stopping,
        Error
    }

    public partial class MainViewModel : ObservableObject
    {
        public const string SearchScopeAllCards = "AllCards";
        public const string SearchScopeDate = "Date";
        public const string SearchScopeCompany = "Company";
        public const string SearchScopeName = "Name";
        public const string SearchScopeTag = "Tag";

        private readonly ITagCatalogService _tagCatalogService;
        private readonly IApplicationSettingsService _applicationSettingsService;
        private readonly ILocalizationService _localizationService;
        private static byte[]? _prototypeScanImageData;
        private ObservableCollection<NavItemModel> _navItems = new();
        public ObservableCollection<NavItemModel> NavItems { get => _navItems; set => SetProperty(ref _navItems, value); }

        public MainViewModel()
        {
            _tagCatalogService = App.GetService<ITagCatalogService>();
            _applicationSettingsService = App.GetService<IApplicationSettingsService>();
            _localizationService = App.GetService<ILocalizationService>();
            InitializeNavigation();
            InitializeSearch();
            _tagCatalogService.TagsChanged += OnTagCatalogChanged;
            _localizationService.LanguageChanged += OnLanguageChanged;
            RefreshTagFilters();
            _isAiEnabled = _applicationSettingsService.IsAiEnabled;

            WeakReferenceMessenger.Default.Register<AutoScanRecognitionCountChangedMessage>(this, (recipient, message) =>
            {
                ApplyAutoScanRecognizingDelta(message.Delta);
            });
        }

        private void InitializeNavigation()
        {
            var recentChildren = new ObservableCollection<NavItemModel>
            {
                new NavItemModel { Title = "Today", IconSource = "Images/Icon_Field.svg" },
                new NavItemModel { Title = "Within 3 days", IconSource = "Images/Icon_Checkmark.svg", IsSelected = true },
                new NavItemModel { Title = "Within 7 days", IconSource = "Images/Icon_Field.svg" }
            };

            NavItems = new ObservableCollection<NavItemModel>
            {
                new NavItemModel { Title = "Recent", IconSource = "Images/Icon_Field.svg", HasChildren = true, Children = recentChildren },
                new NavItemModel { Title = "Company", IconSource = "Images/Icon_Company.svg", HasChildren = true },
                new NavItemModel { Title = "Name", IconSource = "Images/Icon_Name.svg", HasChildren = true },
                new NavItemModel { Title = "Tag", IconSource = "Images/Icon_Tag.svg", HasChildren = true }
            };
        }

        private void InitializeSearch()
        {
            SearchScopeOptions = new ObservableCollection<SearchScopeOption>
            {
                new(SearchScopeAllCards, "Search.Scope.AllCards"),
                new(SearchScopeDate, "Search.Scope.Date"),
                new(SearchScopeCompany, "Search.Scope.Company"),
                new(SearchScopeName, "Search.Scope.Name"),
                new(SearchScopeTag, "Search.Scope.Tag")
            };

            SelectedSearchScope = SearchScopeAllCards;

            RecentSearches = new ObservableCollection<string>
            {
            };
        }

        private bool _skipScanDialog;
        public bool SkipScanDialog
        {
            get => _skipScanDialog;
            set => _skipScanDialog = value;
        }

        private bool _isPaneVisible = true;
        public bool IsPaneVisible
        {
            get => _isPaneVisible;
            set => SetProperty(ref _isPaneVisible, value);
        }

        private bool _isScannerConnected = true;
        public bool IsScannerConnected
        {
            get => _isScannerConnected;
            set
            {
                if (SetProperty(ref _isScannerConnected, value))
                {
                    OnPropertyChanged(nameof(ScannerStatusText));
                    OnPropertyChanged(nameof(ScannerNameVisibility));
                    OnPropertyChanged(nameof(ScannerReadyIndicatorVisibility));
                    OnPropertyChanged(nameof(ScannerOfflineIndicatorVisibility));
                }
            }
        }

        public string ScannerName => "Plustek SmartOffice S602";

        public string ScannerStatusText => IsScannerConnected
            ? _localizationService.GetString("Status.Scanner.Ready")
            : _localizationService.GetString("Status.Scanner.Offline");

        public Microsoft.UI.Xaml.Visibility ScannerNameVisibility =>
            IsScannerConnected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ScannerReadyIndicatorVisibility =>
            IsScannerConnected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ScannerOfflineIndicatorVisibility =>
            IsScannerConnected ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        private IRelayCommand? _toggleScannerStatusCommand;
        public IRelayCommand ToggleScannerStatusCommand => _toggleScannerStatusCommand ??= new RelayCommand(() => IsScannerConnected = !IsScannerConnected);

        private bool _isAiEnabled = true; // Default to ON as per UI
        public bool IsAiEnabled
        {
            get => _isAiEnabled;
            set
            {
                if (SetProperty(ref _isAiEnabled, value))
                {
                    _ = PersistAiEnabledAsync(value);
                    OnPropertyChanged(nameof(AiOnVisibility));
                    OnPropertyChanged(nameof(AiOffVisibility));
                }
            }
        }

        public Microsoft.UI.Xaml.Visibility AiOnVisibility =>
            _isAiEnabled ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility AiOffVisibility =>
            _isAiEnabled ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        private bool _isGridView = true; // Default to Grid view
        public bool IsGridView
        {
            get => _isGridView;
            set => SetProperty(ref _isGridView, value);
        }

        private IRelayCommand? _setGridViewCommand;
        public IRelayCommand SetGridViewCommand => _setGridViewCommand ??= new RelayCommand(() => IsGridView = true);

        private IRelayCommand? _setListViewCommand;
        public IRelayCommand SetListViewCommand => _setListViewCommand ??= new RelayCommand(() => IsGridView = false);

        private ObservableCollection<SearchScopeOption> _searchScopeOptions = new();
        public ObservableCollection<SearchScopeOption> SearchScopeOptions
        {
            get => _searchScopeOptions;
            set => SetProperty(ref _searchScopeOptions, value);
        }

        private string _selectedSearchScope = SearchScopeAllCards;
        public string SelectedSearchScope
        {
            get => _selectedSearchScope;
            set
            {
                if (SetProperty(ref _selectedSearchScope, value))
                {
                    NotifySearchChanged();
                }
            }
        }

        private ObservableCollection<string> _recentSearches = new();
        public ObservableCollection<string> RecentSearches
        {
            get => _recentSearches;
            set => SetProperty(ref _recentSearches, value);
        }

        private ObservableCollection<string> _tagFilters = new();
        public ObservableCollection<string> TagFilters
        {
            get => _tagFilters;
            set => SetProperty(ref _tagFilters, value);
        }

        private string? _selectedTagFilter;
        public string? SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                if (SetProperty(ref _selectedTagFilter, value))
                {
                    NotifySearchChanged();
                }
            }
        }

        private string? _tagSearchKeyword;
        public string? TagSearchKeyword
        {
            get => _tagSearchKeyword;
            set
            {
                if (SetProperty(ref _tagSearchKeyword, string.IsNullOrWhiteSpace(value) ? null : value.Trim()))
                {
                    NotifySearchChanged();
                }
            }
        }

        private IReadOnlyList<string> _advancedTagSearchKeywords = Array.Empty<string>();
        public IReadOnlyList<string> AdvancedTagSearchKeywords
        {
            get => _advancedTagSearchKeywords;
            private set
            {
                var normalized = NormalizeAdvancedTagSearchKeywords(value);
                if (_advancedTagSearchKeywords.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }

                _advancedTagSearchKeywords = normalized;
                OnPropertyChanged(nameof(AdvancedTagSearchKeywords));
                NotifySearchChanged();
            }
        }

        private string? _searchKeyword;
        public string? SearchKeyword
        {
            get => _searchKeyword;
            private set
            {
                if (SetProperty(ref _searchKeyword, NormalizeSearchText(value)))
                {
                    NotifySearchChanged();
                }
            }
        }

        private string? _companySearchKeyword;
        public string? CompanySearchKeyword
        {
            get => _companySearchKeyword;
            private set
            {
                if (SetProperty(ref _companySearchKeyword, NormalizeSearchText(value)))
                {
                    NotifySearchChanged();
                }
            }
        }

        private string? _nameSearchKeyword;
        public string? NameSearchKeyword
        {
            get => _nameSearchKeyword;
            private set
            {
                if (SetProperty(ref _nameSearchKeyword, NormalizeSearchText(value)))
                {
                    NotifySearchChanged();
                }
            }
        }

        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            private set
            {
                if (SetProperty(ref _startDate, value?.Date))
                {
                    NotifySearchChanged();
                }
            }
        }

        private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => _endDate;
            private set
            {
                if (SetProperty(ref _endDate, value?.Date))
                {
                    NotifySearchChanged();
                }
            }
        }

        private string? _selectedRecentPreset;
        public string? SelectedRecentPreset
        {
            get => _selectedRecentPreset;
            private set
            {
                if (SetProperty(ref _selectedRecentPreset, NormalizeSearchText(value)))
                {
                    NotifySearchChanged();
                }
            }
        }

        public bool IsSearchActive =>
            !string.IsNullOrWhiteSpace(SearchKeyword)
            || !string.IsNullOrWhiteSpace(CompanySearchKeyword)
            || !string.IsNullOrWhiteSpace(NameSearchKeyword)
            || !string.IsNullOrWhiteSpace(TagSearchKeyword)
            || AdvancedTagSearchKeywords.Count > 0
            || !string.IsNullOrWhiteSpace(SelectedTagFilter)
            || StartDate.HasValue
            || EndDate.HasValue;

        public string SearchSummaryText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                AddSummaryPart(parts, _localizationService.GetString("Search.Summary.All"), SearchKeyword);
                AddSummaryPart(parts, _localizationService.GetString("Search.Scope.Company"), CompanySearchKeyword);
                AddSummaryPart(parts, _localizationService.GetString("Search.Scope.Name"), NameSearchKeyword);
                var tagSummary = AdvancedTagSearchKeywords.Count > 0
                    ? string.Join(", ", AdvancedTagSearchKeywords)
                    : SelectedTagFilter ?? TagSearchKeyword;
                AddSummaryPart(parts, _localizationService.GetString("Search.Scope.Tag"), tagSummary);

                if (StartDate.HasValue || EndDate.HasValue)
                {
                    var start = StartDate?.ToString("d", _localizationService.CurrentCulture) ?? _localizationService.GetString("Search.Summary.Any");
                    var end = EndDate?.ToString("d", _localizationService.CurrentCulture) ?? _localizationService.GetString("Search.Summary.Any");
                    var label = string.IsNullOrWhiteSpace(SelectedRecentPreset)
                        ? _localizationService.Format("Search.Summary.Range", start, end)
                        : _localizationService.Format("Search.Summary.RangeWithPreset", GetRecentPresetLabel(SelectedRecentPreset), start, end);
                    parts.Add(_localizationService.Format("Search.Summary.Date", label));
                }

                return parts.Count == 0 ? _localizationService.GetString("Search.Summary.Empty") : string.Join(" | ", parts);
            }
        }

        public event Action? TagFilterChanged;
        public event Action? SearchChanged;

        public void AddRecentSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var existing = RecentSearches.FirstOrDefault(x => string.Equals(x, query, System.StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RecentSearches.Remove(existing);
            }

            RecentSearches.Insert(0, query);
            if (RecentSearches.Count > 8)
            {
                RecentSearches.RemoveAt(RecentSearches.Count - 1);
            }
        }

        public void ApplyTagFilter(string? tag)
        {
            ResetSearchState(clearTagFilters: false);
            SelectedTagFilter = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
        }

        public void ApplyTagSearchKeyword(string? keyword)
        {
            ResetSearchState(clearTagFilters: false);
            SelectedTagFilter = null;
            AdvancedTagSearchKeywords = Array.Empty<string>();
            TagSearchKeyword = keyword;
        }

        public void ClearTagFilters()
        {
            SelectedTagFilter = null;
            TagSearchKeyword = null;
            AdvancedTagSearchKeywords = Array.Empty<string>();
        }

        public void ApplyHeaderSearch(string? scope, string? keyword)
        {
            ResetSearchState();
            SelectedSearchScope = string.IsNullOrWhiteSpace(scope) ? SearchScopeAllCards : scope.Trim();

            var normalized = NormalizeSearchText(keyword);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                NotifySearchChanged();
                return;
            }

            switch (SelectedSearchScope)
            {
                case SearchScopeCompany:
                    CompanySearchKeyword = normalized;
                    break;
                case SearchScopeName:
                    NameSearchKeyword = normalized;
                    break;
                case SearchScopeTag:
                    TagSearchKeyword = normalized;
                    break;
                default:
                    SearchKeyword = normalized;
                    break;
            }
        }

        public void ApplyAdvancedSearch(string? company, string? name, string? tag, DateTime? startDate, DateTime? endDate)
        {
            ResetSearchState();
            SelectedSearchScope = SearchScopeAllCards;
            CompanySearchKeyword = company;
            NameSearchKeyword = name;
            TagSearchKeyword = tag;
            ApplyDateRange(startDate, endDate, null);
        }

        public void ApplyAdvancedSearch(string? company, string? name, IEnumerable<string>? tags, DateTime? startDate, DateTime? endDate)
        {
            ResetSearchState();
            SelectedSearchScope = SearchScopeAllCards;
            CompanySearchKeyword = company;
            NameSearchKeyword = name;
            AdvancedTagSearchKeywords = tags?.ToArray() ?? Array.Empty<string>();
            ApplyDateRange(startDate, endDate, null);
        }

        public void ApplyDateRange(DateTime? startDate, DateTime? endDate, string? preset)
        {
            StartDate = startDate?.Date;
            EndDate = endDate?.Date;
            SelectedRecentPreset = preset;
        }

        public void ApplyRecentPreset(string preset)
        {
            var (start, end) = RecentPresetHelper.GetRange(preset);
            ResetSearchState();
            SelectedSearchScope = SearchScopeDate;
            ApplyDateRange(start, end, preset);
        }

        public void ClearSearch()
        {
            ResetSearchState();
            SelectedSearchScope = SearchScopeAllCards;
            NotifySearchChanged();
        }

        private void ResetSearchState(bool clearTagFilters = true)
        {
            SearchKeyword = null;
            CompanySearchKeyword = null;
            NameSearchKeyword = null;
            AdvancedTagSearchKeywords = Array.Empty<string>();
            StartDate = null;
            EndDate = null;
            SelectedRecentPreset = null;

            if (clearTagFilters)
            {
                SelectedTagFilter = null;
                TagSearchKeyword = null;
            }
        }

        private void NotifySearchChanged()
        {
            OnPropertyChanged(nameof(IsSearchActive));
            OnPropertyChanged(nameof(SearchSummaryText));
            TagFilterChanged?.Invoke();
            SearchChanged?.Invoke();
        }

        private static string? NormalizeSearchText(string? value)
        {
            var text = value?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static void AddSummaryPart(System.Collections.Generic.List<string> parts, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{label}: {value}");
            }
        }

        private static IReadOnlyList<string> NormalizeAdvancedTagSearchKeywords(IEnumerable<string>? values)
        {
            return (values ?? Array.Empty<string>())
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
        }

        private void RefreshTagFilters()
        {
            TagFilters = new ObservableCollection<string>(_tagCatalogService.GetAllTags());
        }

        private void OnTagCatalogChanged()
        {
            RefreshTagFilters();
        }

        public System.Func<System.Threading.Tasks.Task<(bool proceed, bool skip)>>? ShowScanConfirmationDialogAsync { get; set; }
        public System.Func<System.Threading.Tasks.Task<bool>>? ShowAiDisabledWarningAsync { get; set; }

        private IAsyncRelayCommand? _scanCommand;
        public IAsyncRelayCommand ScanCommand => _scanCommand ??= new AsyncRelayCommand(ToggleAutoScanAsync);

        private bool _isAutoScanMode;
        public bool IsAutoScanMode
        {
            get => _isAutoScanMode;
            set
            {
                if (SetProperty(ref _isAutoScanMode, value))
                {
                    OnPropertyChanged(nameof(ScanButtonText));
                    OnPropertyChanged(nameof(AutoScanOverlayVisibility));
                    OnPropertyChanged(nameof(AutoScanActiveVisibility));
                }
            }
        }

        private AutoScanState _autoScanState = AutoScanState.Idle;
        public AutoScanState AutoScanState
        {
            get => _autoScanState;
            private set
            {
                if (SetProperty(ref _autoScanState, value))
                {
                    OnPropertyChanged(nameof(AutoScanStateText));
                    OnPropertyChanged(nameof(AutoScanHintText));
                    OnPropertyChanged(nameof(IsAutoScanReady));
                    OnPropertyChanged(nameof(IsAutoScanScanning));
                    OnPropertyChanged(nameof(AutoScanLoadingVisibility));
                    OnPropertyChanged(nameof(AutoScanIdleVisibility));
                    OnPropertyChanged(nameof(AutoScanPhaseText));
                }
            }
        }

        private int _autoScanScannedCount;
        public int AutoScanScannedCount
        {
            get => _autoScanScannedCount;
            private set => SetProperty(ref _autoScanScannedCount, value);
        }

        private int _autoScanRecognizingCount;
        public int AutoScanRecognizingCount
        {
            get => _autoScanRecognizingCount;
            private set => SetProperty(ref _autoScanRecognizingCount, value);
        }

        private int _autoScanFailedCount;
        public int AutoScanFailedCount
        {
            get => _autoScanFailedCount;
            private set => SetProperty(ref _autoScanFailedCount, value);
        }

        private DateTime _lastPaperDetectedAt = DateTime.MinValue;
        private static readonly TimeSpan PaperDebounceInterval = TimeSpan.FromMilliseconds(800);
        private CancellationTokenSource? _autoScanSimulationCts;
        private Task? _autoScanSimulationTask;
        private CancellationTokenSource? _autoScanInstructionCts;

        public string ScanButtonText => IsAutoScanMode
            ? _localizationService.GetString("Button.StopAutoScan")
            : _localizationService.GetString("Button.Scan");

        public Microsoft.UI.Xaml.Visibility AutoScanOverlayVisibility =>
            IsAutoScanMode ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility AutoScanActiveVisibility =>
            IsAutoScanMode ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        private bool _isAutoScanInstructionVisible = true;
        public bool IsAutoScanInstructionVisible
        {
            get => _isAutoScanInstructionVisible;
            private set
            {
                if (SetProperty(ref _isAutoScanInstructionVisible, value))
                {
                    OnPropertyChanged(nameof(AutoScanInstructionVisibility));
                    OnPropertyChanged(nameof(AutoScanCounterVisibility));
                }
            }
        }

        public Microsoft.UI.Xaml.Visibility AutoScanInstructionVisibility =>
            IsAutoScanInstructionVisible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility AutoScanCounterVisibility =>
            IsAutoScanInstructionVisible ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        public bool IsAutoScanReady => IsAutoScanMode && AutoScanState == AutoScanState.Ready;
        public bool IsAutoScanScanning => IsAutoScanMode && AutoScanState == AutoScanState.Scanning;

        public Microsoft.UI.Xaml.Visibility AutoScanLoadingVisibility =>
            IsAutoScanScanning ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility AutoScanIdleVisibility =>
            IsAutoScanScanning ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        public string AutoScanPhaseText => IsAutoScanScanning
            ? _localizationService.GetString("Status.AutoScan.Phase.Scanning")
            : _localizationService.GetString("Status.AutoScan.Phase.Ready");

        public string AutoScanStateText => AutoScanState switch
        {
            AutoScanState.Connecting => _localizationService.GetString("Status.AutoScan.Connecting"),
            AutoScanState.Ready => _localizationService.GetString("Status.AutoScan.Ready"),
            AutoScanState.Scanning => _localizationService.GetString("Status.AutoScan.Scanning"),
            AutoScanState.Stopping => _localizationService.GetString("Status.AutoScan.Stopping"),
            AutoScanState.Error => _localizationService.GetString("Status.AutoScan.Error"),
            _ => _localizationService.GetString("Status.AutoScan.Idle")
        };

        public string AutoScanHintText => AutoScanState switch
        {
            AutoScanState.Connecting => _localizationService.GetString("Status.AutoScan.Hint.Connecting"),
            AutoScanState.Ready => _localizationService.GetString("Status.AutoScan.Hint.Ready"),
            AutoScanState.Scanning => _localizationService.GetString("Status.AutoScan.Hint.Scanning"),
            AutoScanState.Stopping => _localizationService.GetString("Status.AutoScan.Hint.Stopping"),
            AutoScanState.Error => _localizationService.GetString("Status.AutoScan.Hint.Error"),
            _ => _localizationService.GetString("Status.AutoScan.Hint.Idle")
        };

        public event Action? ScanPulseRequested;

        private async Task ToggleAutoScanAsync()
        {
            if (IsAutoScanMode)
            {
                await StopAutoScanAsync();
                return;
            }

            await StartAutoScanAsync();
        }

        private async Task StartAutoScanAsync()
        {
            // 1. Check AI status
            if (!IsAiEnabled && ShowAiDisabledWarningAsync != null)
            {
                var turnOn = await ShowAiDisabledWarningAsync();
                if (turnOn)
                {
                    IsAiEnabled = true;
                }
            }

            // Prototype flow: enter Auto Scan modal directly without pre-confirmation dialog.

            IsAutoScanMode = true;
            IsAutoScanInstructionVisible = true;
            AutoScanScannedCount = 0;
            AutoScanRecognizingCount = 0;
            AutoScanFailedCount = 0;

            AutoScanState = AutoScanState.Ready;
            StartAutoScanInstructionTimer();
            StartAutoScanSimulationLoop();
        }

        private async Task StopAutoScanAsync()
        {
            if (!IsAutoScanMode)
            {
                return;
            }

            StopAutoScanSimulationLoop();
            StopAutoScanInstructionTimer();
            AutoScanState = AutoScanState.Stopping;
            await Task.Delay(200);
            AutoScanState = AutoScanState.Idle;
            IsAutoScanMode = false;
            IsAutoScanInstructionVisible = true;
        }

        private void StartAutoScanInstructionTimer()
        {
            StopAutoScanInstructionTimer();
            _autoScanInstructionCts = new CancellationTokenSource();
            _ = RunAutoScanInstructionTimerAsync(_autoScanInstructionCts.Token);
        }

        private void StopAutoScanInstructionTimer()
        {
            try
            {
                _autoScanInstructionCts?.Cancel();
            }
            catch
            {
            }
            finally
            {
                _autoScanInstructionCts?.Dispose();
                _autoScanInstructionCts = null;
            }
        }

        private async Task RunAutoScanInstructionTimerAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                if (!cancellationToken.IsCancellationRequested && IsAutoScanMode)
                {
                    IsAutoScanInstructionVisible = false;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void StartAutoScanSimulationLoop()
        {
            StopAutoScanSimulationLoop();
            _autoScanSimulationCts = new CancellationTokenSource();
            _autoScanSimulationTask = RunAutoScanSimulationAsync(_autoScanSimulationCts.Token);
        }

        private void StopAutoScanSimulationLoop()
        {
            try
            {
                _autoScanSimulationCts?.Cancel();
            }
            catch
            {
            }
            finally
            {
                _autoScanSimulationCts?.Dispose();
                _autoScanSimulationCts = null;
                _autoScanSimulationTask = null;
            }
        }

        private async Task RunAutoScanSimulationAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsAutoScanMode)
                {
                    AutoScanState = AutoScanState.Ready;
                    await Task.Delay(2000, cancellationToken);
                    if (cancellationToken.IsCancellationRequested || !IsAutoScanMode)
                    {
                        break;
                    }

                    AutoScanState = AutoScanState.Scanning;
                    ScanPulseRequested?.Invoke();
                    await Task.Delay(5000, cancellationToken);
                    if (cancellationToken.IsCancellationRequested || !IsAutoScanMode)
                    {
                        break;
                    }

                    var scanningCard = new BusinessCard
                    {
                        FullName = _localizationService.GetString("Main.Mock.ScannedDocument"),
                        CompanyName = _localizationService.GetString("Processing.Recognizing"),
                        Status = ProcessingStatus.Pending,
                        ScanDate = DateTime.Now,
                        IsAutoScanSession = true,
                        FrontImageData = TryGetPrototypeScanImageData()
                    };

                    AutoScanScannedCount++;
                    WeakReferenceMessenger.Default.Send(new CardsImportedMessage(new System.Collections.Generic.List<BusinessCard> { scanningCard }));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                AutoScanFailedCount++;
                AutoScanState = AutoScanState.Error;
            }
        }

        public async Task TryHandlePaperDetectedAsync()
        {
            if (!IsAutoScanMode || AutoScanState != AutoScanState.Ready)
            {
                return;
            }

            var now = DateTime.Now;
            if (now - _lastPaperDetectedAt < PaperDebounceInterval)
            {
                return;
            }
            _lastPaperDetectedAt = now;

            AutoScanState = AutoScanState.Scanning;
            ScanPulseRequested?.Invoke();

            try
            {
                await Task.Delay(450);

                var scanningCard = new BusinessCard
                {
                    FullName = _localizationService.GetString("Main.Mock.ScannedDocument"),
                    CompanyName = _localizationService.GetString("Processing.Recognizing"),
                    Status = ProcessingStatus.Pending,
                    ScanDate = DateTime.Now,
                    IsAutoScanSession = true,
                    FrontImageData = TryGetPrototypeScanImageData()
                };

                AutoScanScannedCount++;
                WeakReferenceMessenger.Default.Send(new CardsImportedMessage(new System.Collections.Generic.List<BusinessCard> { scanningCard }));
                AutoScanState = AutoScanState.Ready;
            }
            catch
            {
                AutoScanFailedCount++;
                AutoScanState = AutoScanState.Error;
            }
        }

        public async Task RetryAutoScanAsync()
        {
            if (!IsAutoScanMode || AutoScanState != AutoScanState.Error)
            {
                return;
            }

            AutoScanState = AutoScanState.Connecting;
            await Task.Delay(700);
            AutoScanState = AutoScanState.Ready;
        }

        public void MarkRecognizingCompleted(int count = 1)
        {
            if (count <= 0)
            {
                return;
            }

            AutoScanRecognizingCount = Math.Max(0, AutoScanRecognizingCount - count);
        }

        public void MarkScanFailure()
        {
            AutoScanFailedCount++;
        }

        public void PushMockCardOnce()
        {
            var scanningCard = new BusinessCard
            {
                FullName = _localizationService.GetString("Main.Mock.ScannedDocument"),
                CompanyName = _localizationService.GetString("Processing.Recognizing"),
                Status = ProcessingStatus.Pending,
                ScanDate = DateTime.Now,
                IsAutoScanSession = true,
                FrontImageData = TryGetPrototypeScanImageData()
            };

            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(new System.Collections.Generic.List<BusinessCard> { scanningCard }));
        }

        private async Task PersistAiEnabledAsync(bool isAiEnabled)
        {
            try
            {
                await _applicationSettingsService.SetAiEnabledAsync(isAiEnabled);
            }
            catch
            {
            }
        }

        private void ApplyAutoScanRecognizingDelta(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            AutoScanRecognizingCount = Math.Max(0, AutoScanRecognizingCount + delta);
        }

        private static byte[]? TryGetPrototypeScanImageData()
        {
            if (_prototypeScanImageData != null)
            {
                return _prototypeScanImageData.ToArray();
            }

            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "BusinessCard_01.jpg"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "BusinessCard_jp_01.jpg")
            };

            foreach (var path in candidatePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    _prototypeScanImageData = File.ReadAllBytes(path);
                    return _prototypeScanImageData.ToArray();
                }
                catch
                {
                }
            }

            return null;
        }

        private string GetRecentPresetLabel(string? preset)
        {
            return preset switch
            {
                "Today" => _localizationService.GetString("Button.Today"),
                "Within 3 days" => _localizationService.GetString("Button.Within3Days"),
                "Within 7 days" => _localizationService.GetString("Button.Within7Days"),
                _ => preset ?? string.Empty
            };
        }

        private void OnLanguageChanged()
        {
            foreach (var option in SearchScopeOptions)
            {
                option.RefreshLabel();
            }

            OnPropertyChanged(nameof(ScannerStatusText));
            OnPropertyChanged(nameof(SearchSummaryText));
            OnPropertyChanged(nameof(ScanButtonText));
            OnPropertyChanged(nameof(AutoScanPhaseText));
            OnPropertyChanged(nameof(AutoScanStateText));
            OnPropertyChanged(nameof(AutoScanHintText));
        }
    }
}


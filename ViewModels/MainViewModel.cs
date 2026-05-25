#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;

namespace PlustekBCR.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private ObservableCollection<NavItemModel> _navItems = new();
        public ObservableCollection<NavItemModel> NavItems { get => _navItems; set => SetProperty(ref _navItems, value); }

        public MainViewModel()
        {
            InitializeNavigation();
            InitializeSearch();
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
            SearchScopes = new ObservableCollection<string>
            {
                "All cards",
                "Date",
                "Company",
                "Name",
                "Tag"
            };

            SelectedSearchScope = SearchScopes[0];

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

        private bool _isAiEnabled = true; // Default to ON as per UI
        public bool IsAiEnabled
        {
            get => _isAiEnabled;
            set
            {
                if (SetProperty(ref _isAiEnabled, value))
                {
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

        private ObservableCollection<string> _searchScopes = new();
        public ObservableCollection<string> SearchScopes
        {
            get => _searchScopes;
            set => SetProperty(ref _searchScopes, value);
        }

        private string _selectedSearchScope = "所有資料夾";
        public string SelectedSearchScope
        {
            get => _selectedSearchScope;
            set => SetProperty(ref _selectedSearchScope, value);
        }

        private ObservableCollection<string> _recentSearches = new();
        public ObservableCollection<string> RecentSearches
        {
            get => _recentSearches;
            set => SetProperty(ref _recentSearches, value);
        }

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

        public System.Func<System.Threading.Tasks.Task<(bool proceed, bool skip)>>? ShowScanConfirmationDialogAsync { get; set; }
        public System.Func<System.Threading.Tasks.Task<bool>>? ShowAiDisabledWarningAsync { get; set; }

        private IAsyncRelayCommand? _scanCommand;
        public IAsyncRelayCommand ScanCommand => _scanCommand ??= new AsyncRelayCommand(ScanAsync);

        private async System.Threading.Tasks.Task ScanAsync()
        {
            System.Diagnostics.Debug.WriteLine("ScanCommand hit!");

            // 1. Check AI status
            if (!IsAiEnabled && ShowAiDisabledWarningAsync != null)
            {
                var turnOn = await ShowAiDisabledWarningAsync();
                if (turnOn)
                {
                    IsAiEnabled = true;
                }
            }

            // 2. Original scan confirmation logic
            if (!SkipScanDialog && ShowScanConfirmationDialogAsync != null)
            {
                var result = await ShowScanConfirmationDialogAsync();
                if (!result.proceed) return;
                if (result.skip) SkipScanDialog = true;
            }

            // TODO: Implementation of scan logic
            // Add a mock card to show scanning
            var scanningCard = new BusinessCard 
            { 
                Name = "Scanned Document", 
                Company = "Processing...", 
                Status = ProcessingStatus.Recognizing, 
                ScanDate = System.DateTime.Now 
            };
            
            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(new System.Collections.Generic.List<BusinessCard> { scanningCard }));
        }
    }
}


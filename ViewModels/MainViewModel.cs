#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;

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

        public string ScanButtonText => IsAutoScanMode ? "Stop Auto Scan" : "Start Auto Scan";

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

        public string AutoScanPhaseText => IsAutoScanScanning ? "Scanning..." : "Ready for next sheet";

        public string AutoScanStateText => AutoScanState switch
        {
            AutoScanState.Connecting => "Connecting",
            AutoScanState.Ready => "Ready",
            AutoScanState.Scanning => "Scanning",
            AutoScanState.Stopping => "Stopping",
            AutoScanState.Error => "Error",
            _ => "Idle"
        };

        public string AutoScanHintText => AutoScanState switch
        {
            AutoScanState.Connecting => "Establishing AP and scanner connection...",
            AutoScanState.Ready => "Waiting for paper",
            AutoScanState.Scanning => "Paper detected. Capturing scan...",
            AutoScanState.Stopping => "Stopping auto scan session...",
            AutoScanState.Error => "Scanner or sensor is unavailable.",
            _ => "Auto scan is not active."
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
                        Name = "Scanned Document",
                        Company = "Processing...",
                        Status = ProcessingStatus.Recognizing,
                        ScanDate = DateTime.Now,
                        Tag = "AutoScanSession"
                    };

                    AutoScanScannedCount++;
                    AutoScanRecognizingCount++;
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
                    Name = "Scanned Document",
                    Company = "Processing...",
                    Status = ProcessingStatus.Recognizing,
                    ScanDate = DateTime.Now,
                    Tag = "AutoScanSession"
                };

                AutoScanScannedCount++;
                AutoScanRecognizingCount++;

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
                Name = "Scanned Document",
                Company = "Processing...",
                Status = ProcessingStatus.Recognizing,
                ScanDate = DateTime.Now,
                Tag = "AutoScanSession"
            };

            WeakReferenceMessenger.Default.Send(new CardsImportedMessage(new System.Collections.Generic.List<BusinessCard> { scanningCard }));
        }
    }
}

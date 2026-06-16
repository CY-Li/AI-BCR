using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;
using PlustekBCR.Services.Recognition;

namespace PlustekBCR.ViewModels
{
    public partial class AllCardsViewModel : ObservableObject
    {
        private readonly IZipCodeLookupService _zipCodeLookupService;
        private readonly IBusinessCardFieldService _fieldService;
        private readonly JapanZipLookupCoordinator _zipLookupCoordinator;
        private readonly IRecognitionQueueService _recognitionQueueService;
        private readonly ILocalizationService _localizationService;
        private ObservableCollection<BusinessCard> _allCards = new();
        private BusinessCard? _selectedCard;
        private BusinessCard? _subscribedCard;
        private bool _isSidebarOpen;
        private string _editingFieldKey = string.Empty;
        private int _departmentInputCount = 1;
        private bool _isZipLookupInProgress;
        private string _zipLookupStatusMessage = string.Empty;
        private CancellationTokenSource? _zipLookupCts;
        private bool _isApplyingZipLookupResult;

        public ObservableCollection<BusinessCard> AllCards
        {
            get => _allCards;
            set
            {
                if (ReferenceEquals(_allCards, value))
                {
                    return;
                }

                var previousCards = _allCards;
                if (SetProperty(ref _allCards, value))
                {
                    SubscribeToAllCardsCollection(previousCards, value);
                    HandleAllCardsCollectionChanged();
                }
            }
        }

        public BusinessCard? SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (SetProperty(ref _selectedCard, value))
                {
                    SubscribeToSelectedCard(value);
                    EditingFieldKey = string.Empty;
                    SyncDepartmentInputCount(value);
                    ZipLookupStatusMessage = string.Empty;
                    OnPropertyChanged(nameof(HasDetailNameText));
                    OnPropertyChanged(nameof(DetailNameKanaText));
                    OnPropertyChanged(nameof(HasDetailNameKanaText));
                    OnPropertyChanged(nameof(DetailAddressText));
                    OnPropertyChanged(nameof(HasDetailAddressText));
                    OnPropertyChanged(nameof(DetailDepartmentText));
                    OnPropertyChanged(nameof(HasDetailDepartmentText));
                    OnPropertyChanged(nameof(DetailTelephoneText));
                    OnPropertyChanged(nameof(HasDetailTelephoneText));
                }
            }
        }

        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set => SetProperty(ref _isSidebarOpen, value);
        }

        public string EditingFieldKey
        {
            get => _editingFieldKey;
            set
            {
                if (SetProperty(ref _editingFieldKey, value))
                {
                    OnPropertyChanged(nameof(IsEditingName));
                    OnPropertyChanged(nameof(IsEditingDepartment));
                    OnPropertyChanged(nameof(IsEditingAddress));
                    OnPropertyChanged(nameof(IsEditingTelephone));
                }
            }
        }

        public int DepartmentInputCount
        {
            get => _departmentInputCount;
            set
            {
                if (SetProperty(ref _departmentInputCount, value))
                {
                    OnPropertyChanged(nameof(ShowDepartment2));
                    OnPropertyChanged(nameof(ShowDepartment3));
                    OnPropertyChanged(nameof(ShowDepartment4));
                    OnPropertyChanged(nameof(CanAddDepartmentInput));
                }
            }
        }

        public bool IsZipLookupInProgress
        {
            get => _isZipLookupInProgress;
            set => SetProperty(ref _isZipLookupInProgress, value);
        }

        public string ZipLookupStatusMessage
        {
            get => _zipLookupStatusMessage;
            set => SetProperty(ref _zipLookupStatusMessage, value);
        }

        public bool IsEditingName => string.Equals(EditingFieldKey, "Name", StringComparison.Ordinal);
        public bool IsEditingDepartment => string.Equals(EditingFieldKey, "Department", StringComparison.Ordinal);
        public bool IsEditingAddress => string.Equals(EditingFieldKey, "Address", StringComparison.Ordinal);
        public bool IsEditingTelephone => string.Equals(EditingFieldKey, "Telephone", StringComparison.Ordinal);
        public bool ShowDepartment2 => DepartmentInputCount >= 2;
        public bool ShowDepartment3 => DepartmentInputCount >= 3;
        public bool ShowDepartment4 => DepartmentInputCount >= 4;
        public bool CanAddDepartmentInput => DepartmentInputCount < 4;
        public MarketCode CurrentMarket => _fieldService.CurrentMarket;
        public bool IsJapanMarket => CurrentMarket == MarketCode.JP;

        public bool HasDetailNameText => !string.IsNullOrWhiteSpace(SelectedCard?.FullName);
        public string DetailNameKanaText => SelectedCard?.FullNameKana ?? string.Empty;
        public bool HasDetailNameKanaText => !string.IsNullOrWhiteSpace(SelectedCard?.FullNameKana);

        public string DetailDepartmentText =>
            SelectedCard?.DepartmentFull ?? string.Empty;

        public bool HasDetailDepartmentText => !string.IsNullOrWhiteSpace(SelectedCard?.DepartmentFull);
        public bool HasDetailAddressText =>
            !string.IsNullOrWhiteSpace(SelectedCard?.ZipCode)
            || !string.IsNullOrWhiteSpace(SelectedCard?.AddressLine1)
            || !string.IsNullOrWhiteSpace(SelectedCard?.AddressLine2)
            || !string.IsNullOrWhiteSpace(SelectedCard?.City)
            || !string.IsNullOrWhiteSpace(SelectedCard?.State)
            || !string.IsNullOrWhiteSpace(SelectedCard?.Country)
            || !string.IsNullOrWhiteSpace(SelectedCard?.FullAddress);

        public string DetailAddressText
        {
            get
            {
                if (SelectedCard == null)
                {
                    return string.Empty;
                }

                if (CurrentMarket == MarketCode.JP)
                {
                    var segments = new List<string>();
                    if (!string.IsNullOrWhiteSpace(SelectedCard.ZipCode))
                    {
                        segments.Add(SelectedCard.ZipCode.Trim());
                    }

                    if (!string.IsNullOrWhiteSpace(SelectedCard.AddressLine1))
                    {
                        segments.Add(SelectedCard.AddressLine1.Trim());
                    }

                    if (segments.Count > 0)
                    {
                        return string.Join(" ", segments);
                    }
                }

                var composedAddress = BusinessCardAddressHelper.ComposeFullAddress(
                    CurrentMarket,
                    SelectedCard.AddressLine1,
                    SelectedCard.AddressLine2,
                    SelectedCard.City,
                    SelectedCard.State,
                    SelectedCard.ZipCode,
                    SelectedCard.Country);

                if (!string.IsNullOrWhiteSpace(composedAddress))
                {
                    return composedAddress;
                }

                return !string.IsNullOrWhiteSpace(SelectedCard.FullAddress) ? SelectedCard.FullAddress : string.Empty;
            }
        }

        public string DetailTelephoneText
        {
            get
            {
                if (SelectedCard == null)
                {
                    return _localizationService.GetString("Placeholder.ClickToEnterTelephone");
                }

                var tel = (SelectedCard.Tel ?? string.Empty).Trim();
                var extension = (SelectedCard.Extension ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(tel) && string.IsNullOrWhiteSpace(extension))
                {
                    return _localizationService.GetString("Placeholder.ClickToEnterTelephone");
                }

                if (string.IsNullOrWhiteSpace(extension))
                {
                    return tel;
                }

                if (string.IsNullOrWhiteSpace(tel))
                {
                    return extension;
                }

                return $"{tel} ext. {extension}";
            }
        }

        public bool HasDetailTelephoneText =>
            !string.IsNullOrWhiteSpace(SelectedCard?.Tel) || !string.IsNullOrWhiteSpace(SelectedCard?.Extension);

        public MainViewModel MainViewModel { get; }

        public AllCardsViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            _zipCodeLookupService = App.GetService<IZipCodeLookupService>();
            _fieldService = App.GetService<IBusinessCardFieldService>();
            _zipLookupCoordinator = App.GetService<JapanZipLookupCoordinator>();
            _recognitionQueueService = App.GetService<IRecognitionQueueService>();
            _localizationService = App.GetService<ILocalizationService>();
            AllCards = new ObservableCollection<BusinessCard>();
            MainViewModel.SearchChanged += OnSearchChanged;
            _localizationService.LanguageChanged += OnLanguageChanged;

            WeakReferenceMessenger.Default.Register<CardsImportedMessage>(this, (r, m) =>
            {
                App.Window?.DispatcherQueue.TryEnqueue(() =>
                {
                    OnCardsImported(m.Cards);
                });
            });
        }

        private void LoadSampleData()
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            var lastWeek = today.AddDays(-7);

            var images = new[]
            {
                "BusinessCard_jp_01.jpg",
                "BusinessCard_jp_02.jpg",
                "BusinessCard_jp_03.jpg",
                "BusinessCard_jp_04.jpg"
            };

            var cards = new List<BusinessCard>
            {
                new BusinessCard
                {
                    MarketCode = MarketCode.JP,
                    FullName = "大宮 章宏",
                    FirstName = "章宏",
                    LastName = "大宮",
                    CompanyName = "エレコム株式会社",
                    Department1 = "特販東日本支店",
                    Department2 = "特販東日本営業２課",
                    DepartmentFull = "特販東日本支店 / 特販東日本営業２課",
                    JobTitle = "主任",
                    Email = "Akihiro_Omiya@elecom.co.jp",
                    ZipCode = "1010062",
                    AddressLine1 = "東京都千代田区神田駿河台4-6 御茶ノ水ソラシティ16F",
                    FullAddress = "〒101-0062 東京都千代田区神田駿河台4-6 御茶ノ水ソラシティ16F",
                    Tel = "0120-941-149",
                    Fax = "03-6732-9909",
                    Mobile = "090-7487-7145",
                    Website = "www.elecom.co.jp",
                    Status = ProcessingStatus.Recognizing,
                    ScanDate = today
                },
                new BusinessCard
                {
                    MarketCode = MarketCode.JP,
                    FullName = "吉田 亜生",
                    FirstName = "亜生",
                    LastName = "吉田",
                    CompanyName = "コーナン商事株式会社",
                    Department1 = "商品統括部",
                    Department2 = "商品二部",
                    DepartmentFull = "商品統括部 / 商品二部",
                    JobTitle = "電材・照明担当 / バイヤー",
                    Email = "TSUGIO.YOSHIDA@hc-kohnan.co.jp",
                    ZipCode = "5320004",
                    AddressLine1 = "大阪府大阪市淀川区西宮原2丁目2番17号",
                    FullAddress = "〒532-0004 大阪府大阪市淀川区西宮原2丁目2番17号",
                    Tel = "06-6397-1612",
                    Fax = "06-6397-1643",
                    Status = ProcessingStatus.Done,
                    ScanDate = yesterday
                },
                new BusinessCard
                {
                    MarketCode = MarketCode.JP,
                    FullName = "加納 康貴",
                    FirstName = "康貴",
                    LastName = "加納",
                    CompanyName = "大和無線電器株式会社",
                    Department1 = "東日本営業統括部",
                    DepartmentFull = "東日本営業統括部",
                    JobTitle = "主任",
                    Email = "y_kano@dmd.co.jp",
                    ZipCode = "1010021",
                    AddressLine1 = "東京都千代田区外神田5-6-7 関東DGビル3階",
                    FullAddress = "〒101-0021 東京都千代田区外神田5-6-7 関東DGビル3階",
                    Tel = "03-5816-2263",
                    Fax = "03-5816-2272",
                    Mobile = "080-6233-1174",
                    Status = ProcessingStatus.Manual,
                    ScanDate = yesterday
                },
                new BusinessCard
                {
                    MarketCode = MarketCode.JP,
                    FullName = "甚野 慈子",
                    FirstName = "慈子",
                    LastName = "甚野",
                    CompanyName = "株式会社 ダイユーエイト",
                    Department1 = "商品統括部",
                    Department2 = "商品Ⅱ部",
                    DepartmentFull = "商品統括部 / 商品Ⅱ部",
                    JobTitle = "オフィス・OA・一般文具・サービス担当バイヤー",
                    Email = "y-zinno@daiyu8.co.jp",
                    ZipCode = "9608151",
                    AddressLine1 = "福島市太平寺字堰ノ上58番地",
                    FullAddress = "〒960-8151 福島市太平寺字堰ノ上58番地",
                    Tel = "024-545-2216",
                    Fax = "024-545-2504",
                    Website = "https://www.daiyu8.co.jp",
                    Status = ProcessingStatus.Pending,
                    ScanDate = lastWeek
                }
            };

            for (int i = 0; i < cards.Count; i++)
            {
                if (i < images.Length)
                {
                    try
                    {
                        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", images[i]);
                        if (System.IO.File.Exists(path))
                        {
                            cards[i].FrontImageData = System.IO.File.ReadAllBytes(path);
                        }
                        else
                        {
                            var altPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", images[i]);
                            if (System.IO.File.Exists(altPath))
                            {
                                cards[i].FrontImageData = System.IO.File.ReadAllBytes(altPath);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                AllCards.Add(cards[i]);
            }
        }

        public IEnumerable<BusinessCard> FilteredCards => AllCards.Where(MatchesSearch);
        public int FilteredCardCount => FilteredCards.Count();
        public bool HasCards => AllCards.Count > 0;
        public bool ShowEmptyState => !MainViewModel.IsSearchActive && !HasCards;
        public bool ShowCardsContent => FilteredCardCount > 0;

        public string SearchResultSummary => MainViewModel.IsSearchActive
            ? _localizationService.Format("Search.Result.Filtered", FilteredCardCount, FilteredCardCount == 1 ? string.Empty : "s", MainViewModel.SearchSummaryText)
            : _localizationService.Format("Search.Result.Count", AllCards.Count);

        public bool HasNoSearchResults => MainViewModel.IsSearchActive && FilteredCardCount == 0;

        public List<CardGroup> GroupedCards =>
            FilteredCards.OrderByDescending(c => c.ScanDate)
                .GroupBy(c => c.ScanDate.Date)
                .Select(g => new CardGroup(g.Key.ToString("D", _localizationService.CurrentCulture), g))
                .ToList();

        private IRelayCommand? _selectCardCommand;
        public IRelayCommand SelectCardCommand => _selectCardCommand ??= new RelayCommand<BusinessCard>(SelectCard);

        private IRelayCommand? _closeSidebarCommand;
        public IRelayCommand CloseSidebarCommand => _closeSidebarCommand ??= new RelayCommand(CloseSidebar);

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }

        [RelayCommand]
        private async Task DeleteCardAsync(BusinessCard? card)
        {
            if (card == null)
            {
                return;
            }

            if (ConfirmDeleteCardAsync != null)
            {
                var confirm = await ConfirmDeleteCardAsync(card);
                if (confirm)
                {
                    AllCards.Remove(card);
                    RefreshSearchResults();
                    if (SelectedCard == card)
                    {
                        SelectedCard = null;
                        IsSidebarOpen = false;
                    }
                }
            }
        }

        [RelayCommand]
        private void AddDepartmentInput()
        {
            if (DepartmentInputCount < 4)
            {
                DepartmentInputCount++;
            }
        }

        public void BeginFieldEdit(string fieldKey)
        {
            EditingFieldKey = fieldKey;
            if (string.Equals(fieldKey, "Department", StringComparison.Ordinal))
            {
                SyncDepartmentInputCount(SelectedCard);
            }
        }

        public void EndFieldEdit(string fieldKey)
        {
            if (string.Equals(EditingFieldKey, fieldKey, StringComparison.Ordinal))
            {
                EditingFieldKey = string.Empty;
            }
        }

        public async Task LookupZipCodeAsync()
        {
            if (SelectedCard == null || _fieldService.CurrentMarket != MarketCode.JP)
            {
                return;
            }

            if (!JapanZipLookupCoordinator.IsLookupReady(SelectedCard.ZipCode))
            {
                ZipLookupStatusMessage = string.Empty;
                return;
            }

            _zipLookupCts?.Cancel();
            _zipLookupCts = new CancellationTokenSource();

            try
            {
                IsZipLookupInProgress = true;
                ZipLookupStatusMessage = JapanZipLookupCoordinator.LookingUpMessage;

                _isApplyingZipLookupResult = true;
                var outcome = await _zipLookupCoordinator.LookupAndApplyAsync(_zipCodeLookupService, SelectedCard, _zipLookupCts.Token);
                ZipLookupStatusMessage = outcome.StatusMessage;
                OnPropertyChanged(nameof(DetailAddressText));
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isApplyingZipLookupResult = false;
                IsZipLookupInProgress = false;
            }
        }

        private void SelectCard(BusinessCard? card)
        {
            if (card == null)
            {
                return;
            }

            SelectedCard = card;
            IsSidebarOpen = true;
        }

        private void CloseSidebar()
        {
            IsSidebarOpen = false;
            EditingFieldKey = string.Empty;
        }

        private void OnCardsImported(List<BusinessCard> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return;
            }

            var cardsToProcess = new List<BusinessCard>();
            foreach (var card in cards)
            {
                AllCards.Add(card);
                if (card.Status == ProcessingStatus.Recognizing || card.Status == ProcessingStatus.Pending)
                {
                    cardsToProcess.Add(card);
                }
            }

            RefreshSearchResults();

            if (cardsToProcess.Count > 0)
            {
                Task.Run(() => ProcessRecognitionQueueAsync(cardsToProcess));
            }
        }

        private bool MatchesSearch(BusinessCard card)
        {
            if (!MatchesAdvancedFilters(card))
            {
                return false;
            }

            var keyword = MainViewModel.SearchKeyword;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            return MainViewModel.SelectedSearchScope switch
            {
                MainViewModel.SearchScopeCompany => Contains(card.CompanyName, keyword),
                MainViewModel.SearchScopeName => Contains(card.FullName, keyword),
                MainViewModel.SearchScopeTag => ContainsTag(card, keyword, exact: false),
                MainViewModel.SearchScopeDate => true,
                _ => ContainsAnyMainField(card, keyword)
            };
        }

        private bool MatchesAdvancedFilters(BusinessCard card)
        {
            if (!string.IsNullOrWhiteSpace(MainViewModel.CompanySearchKeyword)
                && !Contains(card.CompanyName, MainViewModel.CompanySearchKeyword))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MainViewModel.NameSearchKeyword)
                && !Contains(card.FullName, MainViewModel.NameSearchKeyword))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MainViewModel.SelectedTagFilter)
                && !ContainsTag(card, MainViewModel.SelectedTagFilter, exact: true))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MainViewModel.TagSearchKeyword)
                && !ContainsTag(card, MainViewModel.TagSearchKeyword, exact: false))
            {
                return false;
            }

            if (MainViewModel.AdvancedTagSearchKeywords.Count > 0
                && !ContainsAnySelectedTag(card, MainViewModel.AdvancedTagSearchKeywords))
            {
                return false;
            }

            var scanDate = card.ScanDate.Date;
            if (MainViewModel.StartDate.HasValue && scanDate < MainViewModel.StartDate.Value.Date)
            {
                return false;
            }

            if (MainViewModel.EndDate.HasValue && scanDate > MainViewModel.EndDate.Value.Date)
            {
                return false;
            }

            return true;
        }

        private static bool ContainsAnyMainField(BusinessCard card, string keyword)
        {
            return Contains(card.FullName, keyword)
                || Contains(card.CompanyName, keyword)
                || Contains(card.JobTitle, keyword)
                || Contains(card.Tel, keyword)
                || Contains(card.Mobile, keyword)
                || Contains(card.Email, keyword)
                || Contains(card.FullAddress, keyword)
                || Contains(card.Tag, keyword);
        }

        private static bool Contains(string? source, string? keyword)
        {
            return !string.IsNullOrWhiteSpace(keyword)
                && (source ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsTag(BusinessCard card, string? keyword, bool exact)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            var tags = TagTextHelper.Split(card.Tag);

            return exact
                ? tags.Any(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase))
                : tags.Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAnySelectedTag(BusinessCard card, IReadOnlyList<string> selectedTags)
        {
            if (selectedTags.Count == 0)
            {
                return false;
            }

            var tags = TagTextHelper.Split(card.Tag);

            return tags.Any(cardTag => selectedTags.Any(selectedTag =>
                string.Equals(cardTag, selectedTag, StringComparison.OrdinalIgnoreCase)));
        }

        private void OnSearchChanged()
        {
            SelectedCard = null;
            IsSidebarOpen = false;
            RefreshSearchResults();
        }

        private void SubscribeToAllCardsCollection(ObservableCollection<BusinessCard>? previousCards, ObservableCollection<BusinessCard>? currentCards)
        {
            if (previousCards != null)
            {
                previousCards.CollectionChanged -= OnAllCardsCollectionChanged;
            }

            if (currentCards != null)
            {
                currentCards.CollectionChanged += OnAllCardsCollectionChanged;
            }
        }

        private void OnAllCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            HandleAllCardsCollectionChanged();
        }

        private void HandleAllCardsCollectionChanged()
        {
            if (SelectedCard != null && !AllCards.Contains(SelectedCard))
            {
                SelectedCard = null;
            }

            if (SelectedCard == null && AllCards.Count == 0)
            {
                IsSidebarOpen = false;
            }

            RefreshSearchResults();
        }

        private void RefreshSearchResults()
        {
            OnPropertyChanged(nameof(FilteredCards));
            OnPropertyChanged(nameof(GroupedCards));
            OnPropertyChanged(nameof(FilteredCardCount));
            OnPropertyChanged(nameof(HasCards));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowCardsContent));
            OnPropertyChanged(nameof(SearchResultSummary));
            OnPropertyChanged(nameof(HasNoSearchResults));
        }

        private async Task ProcessRecognitionQueueAsync(List<BusinessCard> cardsToProcess)
        {
            try
            {
                await _recognitionQueueService.EnqueueAsync(cardsToProcess);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing AI recognition: {ex.Message}");
            }
            finally
            {
                App.Window?.DispatcherQueue.TryEnqueue(RefreshSearchResults);
            }
        }

        public async Task ReprocessAiAsync(BusinessCard? card)
        {
            if (card == null || card.Status == ProcessingStatus.Pending || card.Status == ProcessingStatus.Recognizing)
            {
                return;
            }

            if (card.FrontImageData == null || card.FrontImageData.Length == 0)
            {
                WeakReferenceMessenger.Default.Send(new RecognitionWarningMessage(
                    _localizationService.GetString("Recognition.ReprocessUnavailable.Title"),
                    _localizationService.GetString("Recognition.ReprocessUnavailable.Content")));
                return;
            }

            try
            {
                await _recognitionQueueService.EnqueueAsync(card);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reprocessing AI recognition: {ex.Message}");
            }
            finally
            {
                App.Window?.DispatcherQueue.TryEnqueue(RefreshSearchResults);
            }
        }

        private void SubscribeToSelectedCard(BusinessCard? card)
        {
            if (_subscribedCard != null)
            {
                _subscribedCard.PropertyChanged -= OnSelectedCardPropertyChanged;
            }

            _subscribedCard = card;

            if (_subscribedCard != null)
            {
                _subscribedCard.PropertyChanged += OnSelectedCardPropertyChanged;
            }
        }

        private void OnSelectedCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not BusinessCard card)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(BusinessCard.ZipCode):
                    OnPropertyChanged(nameof(HasDetailAddressText));
                    OnPropertyChanged(nameof(DetailAddressText));
                    if (!_isApplyingZipLookupResult && !card.SuppressAutoZipLookup)
                    {
                        _ = TriggerZipLookupAsync(card.ZipCode);
                    }
                    break;
                case nameof(BusinessCard.FullName):
                    OnPropertyChanged(nameof(HasDetailNameText));
                    break;
                case nameof(BusinessCard.FirstNameKana):
                case nameof(BusinessCard.LastNameKana):
                case nameof(BusinessCard.FullNameKana):
                    OnPropertyChanged(nameof(DetailNameKanaText));
                    OnPropertyChanged(nameof(HasDetailNameKanaText));
                    break;
                case nameof(BusinessCard.AddressLine1):
                case nameof(BusinessCard.AddressLine2):
                case nameof(BusinessCard.City):
                case nameof(BusinessCard.State):
                case nameof(BusinessCard.Country):
                case nameof(BusinessCard.FullAddress):
                    OnPropertyChanged(nameof(HasDetailAddressText));
                    OnPropertyChanged(nameof(DetailAddressText));
                    break;
                case nameof(BusinessCard.Tel):
                case nameof(BusinessCard.Extension):
                    OnPropertyChanged(nameof(DetailTelephoneText));
                    OnPropertyChanged(nameof(HasDetailTelephoneText));
                    break;
                case nameof(BusinessCard.Department1):
                case nameof(BusinessCard.Department2):
                case nameof(BusinessCard.Department3):
                case nameof(BusinessCard.Department4):
                case nameof(BusinessCard.DepartmentFull):
                    SyncDepartmentInputCount(card);
                    OnPropertyChanged(nameof(DetailDepartmentText));
                    OnPropertyChanged(nameof(HasDetailDepartmentText));
                    break;
            }
        }

        private async Task TriggerZipLookupAsync(string? zipCode)
        {
            if (_fieldService.CurrentMarket != MarketCode.JP)
            {
                return;
            }

            _zipLookupCts?.Cancel();
            var currentCts = new CancellationTokenSource();
            _zipLookupCts = currentCts;

            try
            {
                await Task.Delay(300, currentCts.Token);
                if (currentCts.IsCancellationRequested)
                {
                    return;
                }

                if (JapanZipLookupCoordinator.IsLookupReady(zipCode))
                {
                    await LookupZipCodeAsync();
                }
                else
                {
                    ZipLookupStatusMessage = string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SyncDepartmentInputCount(BusinessCard? card)
        {
            DepartmentInputCount = BusinessCardDepartmentHelper.GetVisibleDepartmentCount(card);
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(DetailTelephoneText));
            OnPropertyChanged(nameof(SearchResultSummary));
            OnPropertyChanged(nameof(GroupedCards));
        }
    }

    public class CardGroup : List<BusinessCard>
    {
        public string Key { get; set; }

        public CardGroup(string key, IEnumerable<BusinessCard> items)
            : base(items)
        {
            Key = key;
        }
    }
}

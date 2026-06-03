using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;
using PlustekBCR.Views;

namespace PlustekBCR.ViewModels
{
    public partial class AllCardsViewModel : ObservableObject
    {
        private ObservableCollection<BusinessCard> _allCards = new();
        public ObservableCollection<BusinessCard> AllCards { get => _allCards; set => SetProperty(ref _allCards, value); }

        private BusinessCard? _selectedCard;
        public BusinessCard? SelectedCard { get => _selectedCard; set => SetProperty(ref _selectedCard, value); }

        private bool _isSidebarOpen;
        public bool IsSidebarOpen { get => _isSidebarOpen; set => SetProperty(ref _isSidebarOpen, value); }

        public MainViewModel MainViewModel { get; }

        public AllCardsViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            AllCards = new ObservableCollection<BusinessCard>();
            MainViewModel.SearchChanged += OnSearchChanged;
            LoadSampleData();

            // Register for cards imported message
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
                "BusinessCard_01.jpg", 
                "BusinessCard_02.jpg", 
                "BusinessCard_03.jpg",
                "BusinessCard_04.jpg" 
            };

            var cards = new List<BusinessCard>
            {
                new BusinessCard { Name = "Sophia Smith", Company = "RealLiving", Title = "Realtor", Address = "123 Main St, Anytown, USA. 33609", Phone = "804-368-5864", Email = "sophie@mywebsite.com", Status = ProcessingStatus.Recognizing, ScanDate = today },
                new BusinessCard { Name = "James Smith", Company = "RealLiving", Status = ProcessingStatus.Done, ScanDate = yesterday },
                new BusinessCard { Name = "Adam Vincent", Company = "Luxury Estate", Status = ProcessingStatus.Manual, ScanDate = yesterday },
                // new BusinessCard { Name = "張三", Company = "範例股份有限公司", Status = ProcessingStatus.Done, ScanDate = yesterday },
                // new BusinessCard { Name = "李四", Company = "測試有限公司", Status = ProcessingStatus.Done, ScanDate = yesterday },
                new BusinessCard { Name = "Sandra Tucker", Company = "Fine FX", Status = ProcessingStatus.Pending, ScanDate = lastWeek }
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
                            // Try app data or other locations if base directory is different
                            var altPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", images[i]);
                            if (System.IO.File.Exists(altPath))
                            {
                                cards[i].FrontImageData = System.IO.File.ReadAllBytes(altPath);
                            }
                        }
                    }
                    catch { /* Fallback to placeholder in UI */ }
                }
                AllCards.Add(cards[i]);
            }
        }

        public IEnumerable<BusinessCard> FilteredCards => AllCards.Where(MatchesSearch);

        public int FilteredCardCount => FilteredCards.Count();

        public string SearchResultSummary => MainViewModel.IsSearchActive
            ? $"{FilteredCardCount} result{(FilteredCardCount == 1 ? string.Empty : "s")} | {MainViewModel.SearchSummaryText}"
            : $"{AllCards.Count} cards";

        public bool HasNoSearchResults => MainViewModel.IsSearchActive && FilteredCardCount == 0;

        public List<CardGroup> GroupedCards => 
            FilteredCards.OrderByDescending(c => c.ScanDate)
                   .GroupBy(c => c.ScanDate.Date)
                   .Select(g => new CardGroup(g.Key.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture), g))
                   .ToList();

        private IRelayCommand? _selectCardCommand;
        public IRelayCommand SelectCardCommand => _selectCardCommand ??= new RelayCommand<BusinessCard>(SelectCard);

        private IRelayCommand? _closeSidebarCommand;
        public IRelayCommand CloseSidebarCommand => _closeSidebarCommand ??= new RelayCommand(CloseSidebar);

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }

        [RelayCommand]
        private async Task DeleteCardAsync(BusinessCard? card)
        {
            if (card == null) return;
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

        private void SelectCard(BusinessCard? card)
        {
            if (card == null) return;
            SelectedCard = card;
            IsSidebarOpen = true;
        }

        private void CloseSidebar()
        {
            IsSidebarOpen = false;
        }

        private readonly System.Threading.SemaphoreSlim _queueSemaphore = new(3);

        private void OnCardsImported(List<BusinessCard> cards)
        {
            if (cards == null || cards.Count == 0) return;

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
                Task.Run(() => ProcessOcrQueueAsync(cardsToProcess));
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
                "Company" => Contains(card.Company, keyword),
                "Name" => Contains(card.Name, keyword),
                "Tag" => ContainsTag(card, keyword, exact: false),
                "Date" => true,
                _ => ContainsAnyMainField(card, keyword)
            };
        }

        private bool MatchesAdvancedFilters(BusinessCard card)
        {
            if (!string.IsNullOrWhiteSpace(MainViewModel.CompanySearchKeyword)
                && !Contains(card.Company, MainViewModel.CompanySearchKeyword))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MainViewModel.NameSearchKeyword)
                && !Contains(card.Name, MainViewModel.NameSearchKeyword))
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
            return Contains(card.Name, keyword)
                || Contains(card.Company, keyword)
                || Contains(card.Title, keyword)
                || Contains(card.Phone, keyword)
                || Contains(card.Email, keyword)
                || Contains(card.Address, keyword)
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

            var tags = (card.Tag ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            return exact
                ? tags.Any(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase))
                : tags.Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private void OnSearchChanged()
        {
            RefreshSearchResults();
        }

        private void RefreshSearchResults()
        {
            OnPropertyChanged(nameof(FilteredCards));
            OnPropertyChanged(nameof(GroupedCards));
            OnPropertyChanged(nameof(FilteredCardCount));
            OnPropertyChanged(nameof(SearchResultSummary));
            OnPropertyChanged(nameof(HasNoSearchResults));
        }

        private async Task ProcessOcrQueueAsync(List<BusinessCard> cardsToProcess)
        {
            var tasks = cardsToProcess.Select(async card =>
            {
                await _queueSemaphore.WaitAsync();
                try
                {
                    App.Window?.DispatcherQueue.TryEnqueue(() =>
                    {
                        card.Status = ProcessingStatus.Recognizing;
                    });

                    // Simulate AI BCR OCR delay
                    await Task.Delay(3000);

                    string[] names = { "Liam Davis", "Noah Miller", "Oliver Wilson", "Elijah Moore", "William Taylor", "James Anderson", "Benjamin Thomas", "Lucas Jackson", "Henry White", "Alexander Harris" };
                    string[] companies = { "Apex Global", "Zenith Solutions", "BlueSky Tech", "Nova Industries", "Horizon Venture", "Summit Capital", "Quantum Digital", "Pinnacle Group", "Vanguard Media", "Matrix Systems" };
                    string[] titles = { "CEO", "VP of Sales", "Marketing Director", "Lead Engineer", "Product Manager", "Managing Partner", "Chief Architect", "Senior Consultant", "HR Manager", "Creative Director" };

                    var rand = new Random(card.Id.GetHashCode());
                    string randomName = names[rand.Next(names.Length)];
                    string randomCompany = companies[rand.Next(companies.Length)];
                    string randomTitle = titles[rand.Next(titles.Length)];

                    App.Window?.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (string.IsNullOrEmpty(card.Name) || card.Name.StartsWith("BusinessCard") || card.Company == "Ingested Image")
                        {
                            card.Name = randomName;
                            card.Company = randomCompany;
                        }
                        
                        card.Title = randomTitle;
                        card.Email = $"{card.Name.Replace(" ", ".").ToLower()}@{card.Company.Replace(" ", "").ToLower()}.com";
                        card.Phone = $"+1 (555) {rand.Next(100, 999)}-{rand.Next(1000, 9999)}";
                        card.Address = $"{rand.Next(100, 9999)} Silicon Valley Rd, Suite {rand.Next(10, 500)}, San Jose, CA";
                        card.Country = "United States";
                        card.Website = $"www.{card.Company.Replace(" ", "").ToLower()}.com";

                        card.Notes.Add(new Note { Content = "Automatically recognized and parsed by AI BCR Engine." });
                        card.Status = ProcessingStatus.Done;
                        if (card.IsAutoScanSession)
                        {
                            MainViewModel.MarkRecognizingCompleted();
                        }

                        RefreshSearchResults();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing AI OCR: {ex.Message}");
                }
                finally
                {
                    _queueSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
    }

    public class CardGroup : List<BusinessCard>
    {
        public string Key { get; set; }
        public CardGroup(string key, IEnumerable<BusinessCard> items) : base(items)
        {
            Key = key;
        }
    }
}

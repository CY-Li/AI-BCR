using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels
{
    public partial class CardDetailViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<BusinessCard> AllCards { get; set; }

        [ObservableProperty]
        public partial BusinessCard? SelectedCard { get; set; }

        [ObservableProperty]
        public partial string NewNoteContent { get; set; }

        public MainViewModel MainViewModel { get; }
        private readonly ITagCatalogService _tagCatalogService;
        private readonly IZipCodeLookupService _zipCodeLookupService;
        private readonly IBusinessCardFieldService _fieldService;

        private ObservableCollection<BusinessCard>? _originalCards;
        private BusinessCard? _subscribedCard;
        private CancellationTokenSource? _zipLookupCts;
        private bool _isApplyingZipLookupResult;

        [ObservableProperty]
        public partial ObservableCollection<string> AvailableTags { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string> SelectedTags { get; set; }

        [ObservableProperty]
        public partial int DepartmentInputCount { get; set; }

        [ObservableProperty]
        public partial bool IsZipLookupInProgress { get; set; }

        [ObservableProperty]
        public partial string ZipLookupStatusMessage { get; set; }

        public bool ShowDepartment2 => DepartmentInputCount >= 2;
        public bool ShowDepartment3 => DepartmentInputCount >= 3;
        public bool ShowDepartment4 => DepartmentInputCount >= 4;
        public bool CanAddDepartmentInput => DepartmentInputCount < 4;
        public bool IsJapanMarket => _fieldService.CurrentMarket == MarketCode.JP;

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }
        public Action? NavigateBackRequested { get; set; }

        public CardDetailViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            _tagCatalogService = App.GetService<ITagCatalogService>();
            _zipCodeLookupService = App.GetService<IZipCodeLookupService>();
            _fieldService = App.GetService<IBusinessCardFieldService>();
            AllCards = new ObservableCollection<BusinessCard>();
            NewNoteContent = string.Empty;
            AvailableTags = new ObservableCollection<string>(_tagCatalogService.GetAllTags());
            SelectedTags = new ObservableCollection<string>();
            DepartmentInputCount = 1;
            ZipLookupStatusMessage = string.Empty;
            _tagCatalogService.TagsChanged += OnTagCatalogChanged;
        }

        public void Initialize(ObservableCollection<BusinessCard> allCards, BusinessCard? selectedCard)
        {
            _originalCards = allCards;
            AllCards = new ObservableCollection<BusinessCard>(allCards.OrderByDescending(c => c.ScanDate));
            SelectedCard = selectedCard ?? AllCards.FirstOrDefault();
            SyncSelectedTagsFromCard(SelectedCard);
            SyncDepartmentInputCount(SelectedCard);
        }

        partial void OnSelectedCardChanged(BusinessCard? value)
        {
            SubscribeToSelectedCard(value);
            SyncSelectedTagsFromCard(value);
            SyncDepartmentInputCount(value);
        }

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
                    var index = AllCards.IndexOf(card);
                    if (_originalCards != null)
                    {
                        _originalCards.Remove(card);
                    }
                    AllCards.Remove(card);

                    OnPropertyChanged(nameof(GroupedCards));

                    if (AllCards.Count > 0)
                    {
                        var nextIndex = Math.Min(index, AllCards.Count - 1);
                        SelectedCard = AllCards[nextIndex];
                    }
                    else
                    {
                        SelectedCard = null;
                        NavigateBackRequested?.Invoke();
                    }
                }
            }
        }

        public List<CardGroup> GroupedCards =>
            AllCards.OrderByDescending(c => c.ScanDate)
                .GroupBy(c => c.ScanDate.Date)
                .Select(g => new CardGroup(g.Key.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture), g))
                .ToList();

        [RelayCommand]
        private void SelectCard(BusinessCard card)
        {
            SelectedCard = card;
        }

        [RelayCommand]
        private void AddNote()
        {
            if (SelectedCard != null && !string.IsNullOrWhiteSpace(NewNoteContent))
            {
                var note = new Note
                {
                    Content = NewNoteContent,
                    CreatedAt = DateTime.Now
                };

                var notes = new List<Note>(SelectedCard.Notes);
                notes.Insert(0, note);
                SelectedCard.Notes = notes;

                NewNoteContent = string.Empty;
                OnPropertyChanged(nameof(SelectedCard));
            }
        }

        [RelayCommand]
        private void GoBack()
        {
        }

        [RelayCommand]
        private void AddDepartmentInput()
        {
            if (DepartmentInputCount < 4)
            {
                DepartmentInputCount++;
                OnPropertyChanged(nameof(ShowDepartment2));
                OnPropertyChanged(nameof(ShowDepartment3));
                OnPropertyChanged(nameof(ShowDepartment4));
                OnPropertyChanged(nameof(CanAddDepartmentInput));
            }
        }

        public async Task LookupZipCodeAsync()
        {
            if (SelectedCard == null || _fieldService.CurrentMarket != MarketCode.JP)
            {
                return;
            }

            var normalizedZip = (SelectedCard.ZipCode ?? string.Empty).Replace("-", string.Empty).Trim();
            if (normalizedZip.Length != 7)
            {
                ZipLookupStatusMessage = string.Empty;
                return;
            }

            _zipLookupCts?.Cancel();
            _zipLookupCts = new CancellationTokenSource();

            try
            {
                IsZipLookupInProgress = true;
                ZipLookupStatusMessage = "Looking up address...";

                var result = await _zipCodeLookupService.LookupJapanAddressAsync(normalizedZip, _zipLookupCts.Token);
                if (result == null)
                {
                    ZipLookupStatusMessage = "Address not found.";
                    return;
                }

                _isApplyingZipLookupResult = true;
                SelectedCard.MarketCode = MarketCode.JP;
                SelectedCard.ZipCode = result.Zipcode ?? normalizedZip;
                SelectedCard.AddressLine1 = string.Concat(result.Address1 ?? string.Empty, result.Address2 ?? string.Empty, result.Address3 ?? string.Empty);
                SelectedCard.FullAddress = BusinessCardAddressHelper.ComposeFullAddress(SelectedCard.AddressLine1, SelectedCard.AddressLine2, SelectedCard.City, SelectedCard.State, SelectedCard.ZipCode, SelectedCard.Country);
                ZipLookupStatusMessage = "Address updated.";
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                ZipLookupStatusMessage = "Address lookup failed.";
            }
            finally
            {
                _isApplyingZipLookupResult = false;
                IsZipLookupInProgress = false;
            }
        }

        public async Task AddSelectedTagAsync(string? tag)
        {
            var normalized = NormalizeTag(tag);
            if (string.IsNullOrEmpty(normalized) || SelectedCard == null)
            {
                return;
            }

            if (SelectedTags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            SelectedTags.Add(normalized);
            UpdateCardTagString();

            if (_tagCatalogService.AddTag(normalized))
            {
                await _tagCatalogService.SaveAsync();
            }
        }

        public void RemoveSelectedTag(string? tag)
        {
            var normalized = NormalizeTag(tag);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            var target = SelectedTags.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                return;
            }

            SelectedTags.Remove(target);
            UpdateCardTagString();
        }

        private async void SyncSelectedTagsFromCard(BusinessCard? card)
        {
            SelectedTags.Clear();
            if (card == null)
            {
                return;
            }

            foreach (var tag in SplitTags(card.Tag))
            {
                if (SelectedTags.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                SelectedTags.Add(tag);
                if (_tagCatalogService.AddTag(tag))
                {
                    await _tagCatalogService.SaveAsync();
                }
            }
        }

        private void UpdateCardTagString()
        {
            if (SelectedCard == null)
            {
                return;
            }

            SelectedCard.Tag = string.Join(", ", SelectedTags);
        }

        private void OnTagCatalogChanged()
        {
            AvailableTags.Clear();
            foreach (var tag in _tagCatalogService.GetAllTags())
            {
                AvailableTags.Add(tag);
            }
        }

        private void SyncDepartmentInputCount(BusinessCard? card)
        {
            if (card == null)
            {
                DepartmentInputCount = 1;
                OnPropertyChanged(nameof(ShowDepartment2));
                OnPropertyChanged(nameof(ShowDepartment3));
                OnPropertyChanged(nameof(ShowDepartment4));
                OnPropertyChanged(nameof(CanAddDepartmentInput));
                return;
            }

            DepartmentInputCount = 1;
            if (!string.IsNullOrWhiteSpace(card.Department4)) DepartmentInputCount = 4;
            else if (!string.IsNullOrWhiteSpace(card.Department3)) DepartmentInputCount = 3;
            else if (!string.IsNullOrWhiteSpace(card.Department2)) DepartmentInputCount = 2;
            OnPropertyChanged(nameof(ShowDepartment2));
            OnPropertyChanged(nameof(ShowDepartment3));
            OnPropertyChanged(nameof(ShowDepartment4));
            OnPropertyChanged(nameof(CanAddDepartmentInput));
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
            if (_isApplyingZipLookupResult || sender is not BusinessCard card)
            {
                return;
            }

            if (e.PropertyName == nameof(BusinessCard.ZipCode))
            {
                _ = TriggerZipLookupAsync(card.ZipCode);
            }
        }

        private async Task TriggerZipLookupAsync(string? zipCode)
        {
            _zipLookupCts?.Cancel();

            var normalizedZip = (zipCode ?? string.Empty).Replace("-", string.Empty).Trim();
            if (normalizedZip.Length != 7)
            {
                ZipLookupStatusMessage = string.Empty;
                IsZipLookupInProgress = false;
                return;
            }

            _zipLookupCts = new CancellationTokenSource();
            var cancellationToken = _zipLookupCts.Token;

            try
            {
                await Task.Delay(300, cancellationToken);
                await LookupZipCodeAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static List<string> SplitTags(string? rawTags)
        {
            return (rawTags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeTag)
                .Where(x => !string.IsNullOrEmpty(x))
                .Cast<string>()
                .ToList();
        }

        private static string NormalizeTag(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}

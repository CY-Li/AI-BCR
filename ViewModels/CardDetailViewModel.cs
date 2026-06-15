using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly JapanZipLookupCoordinator _zipLookupCoordinator;
        private readonly IRecognitionQueueService _recognitionQueueService;

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
        public MarketCode CurrentMarket => _fieldService.CurrentMarket;
        public bool IsJapanMarket => CurrentMarket == MarketCode.JP;

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }
        public Action? NavigateBackRequested { get; set; }

        public CardDetailViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            _tagCatalogService = App.GetService<ITagCatalogService>();
            _zipCodeLookupService = App.GetService<IZipCodeLookupService>();
            _fieldService = App.GetService<IBusinessCardFieldService>();
            _zipLookupCoordinator = App.GetService<JapanZipLookupCoordinator>();
            _recognitionQueueService = App.GetService<IRecognitionQueueService>();
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

        public async Task AddSelectedTagAsync(string? tag)
        {
            var normalized = NormalizeTag(tag);
            if (string.IsNullOrEmpty(normalized) || SelectedCard == null)
            {
                return;
            }

            if (TagTextHelper.ContainsIgnoreCase(SelectedTags, normalized))
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

        public async Task ReprocessAiAsync(BusinessCard? card)
        {
            if (card == null || card.Status == ProcessingStatus.Recognizing)
            {
                return;
            }

            if (card.FrontImageData == null || card.FrontImageData.Length == 0)
            {
                WeakReferenceMessenger.Default.Send(new RecognitionWarningMessage(
                    "AI re-recognition unavailable",
                    "AI re-recognition requires a front card image."));
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
                RefreshSelectedCardDetails();
            }
        }

        public void RemoveSelectedTag(string? tag)
        {
            if (!TagTextHelper.RemoveFirstIgnoreCase(SelectedTags, tag))
            {
                return;
            }

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
                if (!TagTextHelper.AddIfMissing(SelectedTags, tag))
                {
                    continue;
                }
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

            SelectedCard.Tag = TagTextHelper.Join(SelectedTags);
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
            DepartmentInputCount = BusinessCardDepartmentHelper.GetVisibleDepartmentCount(card);
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

            if (e.PropertyName == nameof(BusinessCard.ZipCode) && !card.SuppressAutoZipLookup)
            {
                _ = TriggerZipLookupAsync(card.ZipCode);
            }
        }

        private async Task TriggerZipLookupAsync(string? zipCode)
        {
            _zipLookupCts?.Cancel();

            if (!JapanZipLookupCoordinator.IsLookupReady(zipCode))
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
            return TagTextHelper.Split(rawTags).ToList();
        }

        private static string NormalizeTag(string? value)
        {
            return TagTextHelper.Normalize(value);
        }

        private void RefreshSelectedCardDetails()
        {
            SyncDepartmentInputCount(SelectedCard);
        }
    }
}

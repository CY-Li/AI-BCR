using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        private ObservableCollection<BusinessCard>? _originalCards;

        [ObservableProperty]
        public partial ObservableCollection<string> AvailableTags { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string> SelectedTags { get; set; }

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }
        public Action? NavigateBackRequested { get; set; }

        public CardDetailViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            _tagCatalogService = App.GetService<ITagCatalogService>();
            AllCards = new ObservableCollection<BusinessCard>();
            NewNoteContent = string.Empty;
            AvailableTags = new ObservableCollection<string>(_tagCatalogService.GetAllTags());
            SelectedTags = new ObservableCollection<string>();
            _tagCatalogService.TagsChanged += OnTagCatalogChanged;
        }

        public void Initialize(ObservableCollection<BusinessCard> allCards, BusinessCard? selectedCard)
        {
            _originalCards = allCards;
            AllCards = new ObservableCollection<BusinessCard>(allCards.OrderByDescending(c => c.ScanDate));
            SelectedCard = selectedCard ?? AllCards.FirstOrDefault();
            SyncSelectedTagsFromCard(SelectedCard);
        }

        partial void OnSelectedCardChanged(BusinessCard? value)
        {
            SyncSelectedTagsFromCard(value);
        }

        [RelayCommand]
        private async Task DeleteCardAsync(BusinessCard? card)
        {
            if (card == null) return;
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
                
                // Note: BusinessCard.Notes is a List<Note>, might need to be ObservableCollection or handled manually for UI update
                var notes = new List<Note>(SelectedCard.Notes);
                notes.Insert(0, note); // Insert at top for timeline
                SelectedCard.Notes = notes;
                
                NewNoteContent = string.Empty;
                OnPropertyChanged(nameof(SelectedCard));
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            // This will be handled in the View or via a NavigationService if available
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

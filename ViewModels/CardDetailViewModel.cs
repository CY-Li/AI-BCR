using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Models;

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

        private ObservableCollection<BusinessCard>? _originalCards;

        public Func<BusinessCard, Task<bool>>? ConfirmDeleteCardAsync { get; set; }
        public Action? NavigateBackRequested { get; set; }

        public CardDetailViewModel()
        {
            MainViewModel = App.GetService<MainViewModel>();
            AllCards = new ObservableCollection<BusinessCard>();
            NewNoteContent = string.Empty;
        }

        public void Initialize(ObservableCollection<BusinessCard> allCards, BusinessCard? selectedCard)
        {
            _originalCards = allCards;
            AllCards = new ObservableCollection<BusinessCard>(allCards.OrderByDescending(c => c.ScanDate));
            SelectedCard = selectedCard ?? AllCards.FirstOrDefault();
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
    }
}

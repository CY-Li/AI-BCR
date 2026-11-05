using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Models;

namespace PlustekBCR.ViewModels
{
    public sealed partial class NoteEditorItemViewModel : ObservableObject
    {
        private readonly Func<BusinessCard?> _selectedCardProvider;
        private readonly Action<NoteEditorItemViewModel> _beginEditRequested;
        private bool _isEditing;
        private string _draftContent = string.Empty;

        public NoteEditorItemViewModel(
            Note note,
            Func<BusinessCard?> selectedCardProvider,
            Action<NoteEditorItemViewModel> beginEditRequested)
        {
            Note = note;
            _selectedCardProvider = selectedCardProvider;
            _beginEditRequested = beginEditRequested;
        }

        public Note Note { get; }

        public bool IsEditing
        {
            get => _isEditing;
            private set => SetProperty(ref _isEditing, value);
        }

        public string DraftContent
        {
            get => _draftContent;
            set
            {
                if (SetProperty(ref _draftContent, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public bool CanSave => !string.IsNullOrWhiteSpace(DraftContent);

        [RelayCommand]
        private void BeginEdit()
        {
            if (_selectedCardProvider()?.Notes.Contains(Note) != true)
            {
                return;
            }

            _beginEditRequested(this);
            DraftContent = Note.Content;
            IsEditing = true;
        }

        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
            DraftContent = string.Empty;
        }

        [RelayCommand]
        private void SaveEdit()
        {
            if (!IsEditing || !CanSave)
            {
                return;
            }

            var card = _selectedCardProvider();
            if (card?.Notes.Contains(Note) != true)
            {
                CancelEdit();
                return;
            }

            var content = DraftContent.Trim();
            CancelEdit();
            if (content == Note.Content)
            {
                return;
            }

            Note.UpdateContent(content);
            card.Notes = new List<Note>(card.Notes);
        }
    }
}

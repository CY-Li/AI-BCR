using PlustekBCR.Models;
using PlustekBCR.ViewModels;
using Xunit;

namespace PlustekBCR.Tests;

public sealed class NoteEditorItemViewModelTests
{
    [Fact]
    public void SaveEdit_TrimsContentAndUpdatesTimestampWithoutChangingIdentity()
    {
        var createdAt = new DateTime(2026, 11, 5, 9, 0, 0);
        var note = new Note { Content = "Original", CreatedAt = createdAt };
        var card = new BusinessCard { Notes = new List<Note> { note } };
        var originalList = card.Notes;
        var item = new NoteEditorItemViewModel(note, () => card, _ => { });

        item.BeginEditCommand.Execute(null);
        item.DraftContent = "  Revised note  ";
        item.SaveEditCommand.Execute(null);

        Assert.Equal("Revised note", note.Content);
        Assert.Equal(createdAt, note.CreatedAt);
        Assert.NotNull(note.UpdatedAt);
        Assert.Equal(note.UpdatedAt, card.LatestNoteModifiedAt);
        Assert.NotSame(originalList, card.Notes);
        Assert.Same(note, Assert.Single(card.Notes));
        Assert.False(item.IsEditing);
    }

    [Fact]
    public void SaveEdit_UnchangedContentPreservesTimestampAndNotesList()
    {
        var note = new Note { Content = "Original" };
        var card = new BusinessCard { Notes = new List<Note> { note } };
        var originalList = card.Notes;
        var item = new NoteEditorItemViewModel(note, () => card, _ => { });

        item.BeginEditCommand.Execute(null);
        item.DraftContent = " Original ";
        item.SaveEditCommand.Execute(null);

        Assert.Null(note.UpdatedAt);
        Assert.Same(originalList, card.Notes);
        Assert.False(item.IsEditing);
    }

    [Fact]
    public void BlankDraftCannotSaveAndCancelPreservesNote()
    {
        var note = new Note { Content = "Original" };
        var card = new BusinessCard { Notes = new List<Note> { note } };
        var item = new NoteEditorItemViewModel(note, () => card, _ => { });

        item.BeginEditCommand.Execute(null);
        item.DraftContent = "  ";
        Assert.False(item.CanSave);
        item.SaveEditCommand.Execute(null);
        Assert.True(item.IsEditing);

        item.CancelEditCommand.Execute(null);
        Assert.Equal("Original", note.Content);
        Assert.Null(note.UpdatedAt);
        Assert.False(item.IsEditing);
    }

    [Fact]
    public void SaveEdit_DoesNotChangeNoteAfterCardSelectionChanges()
    {
        var note = new Note { Content = "Original" };
        var selectedCard = new BusinessCard { Notes = new List<Note> { note } };
        var item = new NoteEditorItemViewModel(note, () => selectedCard, _ => { });

        item.BeginEditCommand.Execute(null);
        item.DraftContent = "Changed";
        selectedCard = new BusinessCard();
        item.SaveEditCommand.Execute(null);

        Assert.Equal("Original", note.Content);
        Assert.False(item.IsEditing);
    }
}

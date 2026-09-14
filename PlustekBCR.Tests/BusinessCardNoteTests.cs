using PlustekBCR.Models;
using Xunit;

namespace PlustekBCR.Tests;

public class BusinessCardNoteTests
{
    [Fact]
    public void LatestNoteModifiedAt_UsesCreatedAtForLegacyNotes()
    {
        var createdAt = new DateTime(2026, 9, 11, 9, 30, 0);
        var card = new BusinessCard
        {
            Notes = new List<Note>
            {
                new() { CreatedAt = createdAt, Content = "Legacy note" }
            }
        };

        Assert.True(card.HasNotes);
        Assert.Equal(createdAt, card.LatestNoteModifiedAt);
    }

    [Fact]
    public void LatestNoteModifiedAt_UsesMostRecentEditTime()
    {
        var card = new BusinessCard
        {
            Notes = new List<Note>
            {
                new()
                {
                    CreatedAt = new DateTime(2026, 9, 10, 9, 0, 0),
                    UpdatedAt = new DateTime(2026, 9, 11, 14, 15, 0),
                    Content = "Edited note"
                },
                new()
                {
                    CreatedAt = new DateTime(2026, 9, 11, 10, 0, 0),
                    Content = "Newer but unedited note"
                }
            }
        };

        Assert.Equal(new DateTime(2026, 9, 11, 14, 15, 0), card.LatestNoteModifiedAt);
    }

    [Fact]
    public void LatestNoteModifiedAt_NotifiesWhenNoteIsEdited()
    {
        var note = new Note
        {
            CreatedAt = new DateTime(2026, 9, 11, 9, 0, 0),
            Content = "Original"
        };
        var card = new BusinessCard { Notes = new List<Note> { note } };
        var changedProperties = new List<string?>();
        card.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        note.UpdatedAt = new DateTime(2026, 9, 11, 16, 45, 0);

        Assert.Equal(note.UpdatedAt, card.LatestNoteModifiedAt);
        Assert.Contains(nameof(BusinessCard.LatestNoteModifiedAt), changedProperties);
    }
}

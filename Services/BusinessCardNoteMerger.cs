using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public static class BusinessCardNoteMerger
    {
        public static void MergeInto(BusinessCard retainedCard, IEnumerable<BusinessCard> replacedCards)
        {
            ArgumentNullException.ThrowIfNull(retainedCard);
            ArgumentNullException.ThrowIfNull(replacedCards);

            retainedCard.Notes = (retainedCard.Notes ?? new List<Note>())
                .Concat(replacedCards
                    .Where(card => card != null)
                    .SelectMany(card => card.Notes ?? new List<Note>()))
                .GroupBy(note => note.Id)
                .Select(group => group.First())
                .OrderByDescending(note => note.LastModifiedAt)
                .ToList();
        }
    }
}

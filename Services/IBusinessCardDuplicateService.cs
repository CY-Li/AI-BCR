using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IBusinessCardDuplicateService
    {
        IReadOnlyList<DuplicateMatchResult> FindMatches(
            BusinessCard candidate,
            IEnumerable<BusinessCard> existingCards,
            DuplicateComparisonSettings settings);

        bool IsSupportedField(string fieldKey);
    }
}

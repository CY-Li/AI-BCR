using PlustekBCR.Models;
using PlustekBCR.Services;
using Xunit;

namespace PlustekBCR.Tests;

public sealed class BusinessCardDuplicateServiceTests
{
    private readonly BusinessCardDuplicateService _service = new();

    [Fact]
    public void EmailComparison_IgnoresCaseWhitespaceAndUnicodeWidth()
    {
        var existing = Card(email: "USER@example.com");
        var candidate = Card(email: "  ｕｓｅｒ＠ｅｘａｍｐｌｅ．ｃｏｍ  ");

        var result = Find(candidate, new[] { existing }, DuplicateMatchOperator.Or, "email");

        Assert.Single(result);
        Assert.Equal("email", Assert.Single(result[0].MatchedFields));
    }

    [Fact]
    public void PhoneComparison_IgnoresCommonFormatting()
    {
        var existing = Card(tel: "+886 (2) 1234-5678");
        var candidate = Card(tel: "+886212345678");

        Assert.Single(Find(candidate, new[] { existing }, DuplicateMatchOperator.Or, "tel"));
    }

    [Fact]
    public void EmptyValues_NeverMatch()
    {
        Assert.Empty(Find(Card(), new[] { Card() }, DuplicateMatchOperator.Or, "email"));
    }

    [Fact]
    public void Or_MatchesWhenAnySelectedFieldMatches()
    {
        var existing = Card(email: "same@example.com", company: "Old Company");
        var candidate = Card(email: "same@example.com", company: "New Company");

        Assert.Single(Find(candidate, new[] { existing }, DuplicateMatchOperator.Or, "email", "company_name"));
    }

    [Fact]
    public void And_RequiresEverySelectedFieldToBePresentAndMatch()
    {
        var existing = Card(email: "same@example.com", company: "Company");
        var partial = Card(email: "same@example.com");
        var complete = Card(email: "same@example.com", company: " company ");

        Assert.Empty(Find(partial, new[] { existing }, DuplicateMatchOperator.And, "email", "company_name"));
        Assert.Single(Find(complete, new[] { existing }, DuplicateMatchOperator.And, "email", "company_name"));
    }

    [Fact]
    public void Comparison_ExcludesCandidateItself()
    {
        var candidate = Card(email: "same@example.com");

        Assert.Empty(Find(candidate, new[] { candidate }, DuplicateMatchOperator.Or, "email"));
    }

    [Fact]
    public void Comparison_ReturnsEveryMatchingCandidate()
    {
        var candidate = Card(email: "same@example.com");
        var existing = new[]
        {
            Card(email: "same@example.com"),
            Card(email: "other@example.com"),
            Card(email: "SAME@example.com")
        };

        Assert.Equal(2, Find(candidate, existing, DuplicateMatchOperator.Or, "email").Count);
    }

    [Fact]
    public void GeneralText_CollapsesWhitespaceAndUsesExactNormalizedValue()
    {
        var candidate = Card(company: "Plustek   Inc.");

        Assert.Single(Find(candidate, new[] { Card(company: " plustek inc. ") }, DuplicateMatchOperator.Or, "company_name"));
        Assert.Empty(Find(candidate, new[] { Card(company: "Plustek") }, DuplicateMatchOperator.Or, "company_name"));
    }

    [Fact]
    public void UnsupportedConfiguration_FallsBackToEmail()
    {
        var candidate = Card(email: "same@example.com");

        Assert.Single(Find(candidate, new[] { Card(email: "same@example.com") }, DuplicateMatchOperator.Or, "unknown_field"));
    }

    [Fact]
    public void Rebuild_AfterDeletingOnlyTarget_ClearsPendingState()
    {
        var existing = Card(email: "same@example.com");
        var candidate = Card(email: "same@example.com");
        var cards = new List<BusinessCard> { existing, candidate };

        Rebuild(cards, "email");
        Assert.True(candidate.IsDuplicatePending);

        cards.Remove(existing);
        Rebuild(cards, "email");

        Assert.Equal(DuplicateReviewState.None, candidate.DuplicateReviewState);
        Assert.Empty(candidate.DuplicateMatches);
    }

    [Fact]
    public void Rebuild_ThreeDuplicatesAfterDeletingFirst_PreservesForwardOnlyRelationship()
    {
        var first = Card(email: "same@example.com");
        var second = Card(email: "same@example.com");
        var third = Card(email: "same@example.com");
        var cards = new List<BusinessCard> { first, second, third };

        Rebuild(cards, "email");
        Assert.Equal(new[] { first }, second.DuplicateMatches.Select(match => match.ExistingCard));
        Assert.Equal(new[] { first, second }, third.DuplicateMatches.Select(match => match.ExistingCard));

        cards.Remove(first);
        Rebuild(cards, "email");

        Assert.Empty(second.DuplicateMatches);
        Assert.Equal(DuplicateReviewState.None, second.DuplicateReviewState);
        Assert.Equal(new[] { second }, third.DuplicateMatches.Select(match => match.ExistingCard));
        Assert.DoesNotContain(third.DuplicateMatches, match => !cards.Contains(match.ExistingCard));
    }

    [Fact]
    public void Rebuild_AfterDeletingMiddleCard_ReassignsLaterCandidatesInCollectionOrder()
    {
        var first = Card(email: "same@example.com");
        var middle = Card(email: "same@example.com");
        var third = Card(email: "same@example.com");
        var fourth = Card(email: "same@example.com");
        var cards = new List<BusinessCard> { first, middle, third, fourth };

        Rebuild(cards, "email");
        cards.Remove(middle);
        Rebuild(cards, "email");

        Assert.Empty(first.DuplicateMatches);
        Assert.Equal(new[] { first }, third.DuplicateMatches.Select(match => match.ExistingCard));
        Assert.Equal(new[] { first, third }, fourth.DuplicateMatches.Select(match => match.ExistingCard));
        Assert.DoesNotContain(
            cards.SelectMany(card => card.DuplicateMatches),
            match => ReferenceEquals(match.ExistingCard, middle));
    }

    [Fact]
    public void Rebuild_PreservesAcceptedCardAndUsesItAsLaterBaseline()
    {
        var removedBaseline = Card(email: "same@example.com");
        var accepted = Card(email: "same@example.com");
        var later = Card(email: "same@example.com");
        accepted.DuplicateReviewState = DuplicateReviewState.Accepted;
        var cards = new List<BusinessCard> { removedBaseline, accepted, later };

        cards.Remove(removedBaseline);
        Rebuild(cards, "email");

        Assert.Equal(DuplicateReviewState.Accepted, accepted.DuplicateReviewState);
        Assert.Empty(accepted.DuplicateMatches);
        Assert.Equal(new[] { accepted }, later.DuplicateMatches.Select(match => match.ExistingCard));
    }

    [Fact]
    public void Rebuild_SettingChangeCanDiscoverPreviouslyUnmatchedCard()
    {
        var first = Card(email: "first@example.com", company: "Same Company");
        var second = Card(email: "second@example.com", company: "Same Company");
        var cards = new List<BusinessCard> { first, second };

        Rebuild(cards, "email");
        Assert.Equal(DuplicateReviewState.None, second.DuplicateReviewState);

        Rebuild(cards, "company_name");
        Assert.True(second.IsDuplicatePending);
        Assert.Equal("company_name", Assert.Single(second.DuplicateMatches[0].MatchedFields));
    }

    [Fact]
    public void Rebuild_SkipsCardsUntilRecognitionIsComplete()
    {
        var pending = Card(email: "same@example.com");
        pending.Status = ProcessingStatus.Pending;
        var recognizing = Card(email: "same@example.com");
        recognizing.Status = ProcessingStatus.Recognizing;
        var completed = Card(email: "same@example.com");
        var cards = new List<BusinessCard> { pending, recognizing, completed };

        Rebuild(cards, "email");

        Assert.Equal(DuplicateReviewState.None, pending.DuplicateReviewState);
        Assert.Equal(DuplicateReviewState.None, recognizing.DuplicateReviewState);
        Assert.Equal(DuplicateReviewState.None, completed.DuplicateReviewState);
    }

    private IReadOnlyList<DuplicateMatchResult> Find(
        BusinessCard candidate,
        IEnumerable<BusinessCard> existing,
        DuplicateMatchOperator matchOperator,
        params string[] fields)
    {
        return _service.FindMatches(candidate, existing, new DuplicateComparisonSettings
        {
            MatchOperator = matchOperator,
            Fields = fields.ToList()
        });
    }

    private void Rebuild(IReadOnlyList<BusinessCard> cards, params string[] fields)
    {
        _service.RebuildReviewStates(cards, new DuplicateComparisonSettings
        {
            MatchOperator = DuplicateMatchOperator.Or,
            Fields = fields.ToList()
        });
    }

    private static BusinessCard Card(string email = "", string tel = "", string company = "") => new()
    {
        Email = email,
        Tel = tel,
        CompanyName = company,
        Status = ProcessingStatus.Done
    };
}

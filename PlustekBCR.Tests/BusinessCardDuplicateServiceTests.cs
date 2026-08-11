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

    private static BusinessCard Card(string email = "", string tel = "", string company = "") => new()
    {
        Email = email,
        Tel = tel,
        CompanyName = company,
        Status = ProcessingStatus.Done
    };
}

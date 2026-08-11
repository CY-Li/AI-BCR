using System;
using System.Collections.Generic;

namespace PlustekBCR.Models
{
    public enum DuplicateMatchOperator
    {
        Or,
        And
    }

    public enum DuplicateReviewState
    {
        None,
        Pending,
        Accepted
    }

    public sealed class DuplicateComparisonSettings
    {
        public static IReadOnlySet<string> SupportedFieldKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "company_name", "department_1", "department_2", "department_3", "department_4",
            "department_full", "job_title", "last_name", "last_name_kana", "middle_name",
            "first_name", "first_name_kana", "suffix", "full_name", "full_name_kana",
            "zip_code", "country", "state", "city", "address_line_1", "address_line_2",
            "full_address", "tel", "extension", "fax", "mobile", "email", "website"
        };

        public static DuplicateComparisonSettings Default => new();

        public DuplicateMatchOperator MatchOperator { get; set; } = DuplicateMatchOperator.Or;
        public List<string> Fields { get; set; } = new() { "email" };

        public DuplicateComparisonSettings Clone() => new()
        {
            MatchOperator = MatchOperator,
            Fields = new List<string>(Fields)
        };
    }

    public sealed class DuplicateMatchResult
    {
        public required BusinessCard ExistingCard { get; init; }
        public required IReadOnlyList<string> MatchedFields { get; init; }

        public string MatchedFieldKeys => string.Join(", ", MatchedFields);
        public string ExistingDisplayName => ExistingCard.DisplayName;
        public string ExistingCompanyName => ExistingCard.CompanyName;
    }

    public sealed class BusinessCardRecognitionCompletedMessage
    {
        public BusinessCardRecognitionCompletedMessage(BusinessCard card)
        {
            Card = card;
        }

        public BusinessCard Card { get; }
    }
}

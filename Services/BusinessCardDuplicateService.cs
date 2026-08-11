using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PlustekBCR.Helpers;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public sealed class BusinessCardDuplicateService : IBusinessCardDuplicateService
    {
        private static readonly IReadOnlyDictionary<string, string> PropertyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["company_name"] = nameof(BusinessCard.CompanyName),
                ["department_1"] = nameof(BusinessCard.Department1),
                ["department_2"] = nameof(BusinessCard.Department2),
                ["department_3"] = nameof(BusinessCard.Department3),
                ["department_4"] = nameof(BusinessCard.Department4),
                ["department_full"] = nameof(BusinessCard.DepartmentFull),
                ["job_title"] = nameof(BusinessCard.JobTitle),
                ["last_name"] = nameof(BusinessCard.LastName),
                ["last_name_kana"] = nameof(BusinessCard.LastNameKana),
                ["middle_name"] = nameof(BusinessCard.MiddleName),
                ["first_name"] = nameof(BusinessCard.FirstName),
                ["first_name_kana"] = nameof(BusinessCard.FirstNameKana),
                ["suffix"] = nameof(BusinessCard.Suffix),
                ["full_name"] = nameof(BusinessCard.FullName),
                ["full_name_kana"] = nameof(BusinessCard.FullNameKana),
                ["zip_code"] = nameof(BusinessCard.ZipCode),
                ["country"] = nameof(BusinessCard.Country),
                ["state"] = nameof(BusinessCard.State),
                ["city"] = nameof(BusinessCard.City),
                ["address_line_1"] = nameof(BusinessCard.AddressLine1),
                ["address_line_2"] = nameof(BusinessCard.AddressLine2),
                ["full_address"] = nameof(BusinessCard.FullAddress),
                ["tel"] = nameof(BusinessCard.Tel),
                ["extension"] = nameof(BusinessCard.Extension),
                ["fax"] = nameof(BusinessCard.Fax),
                ["mobile"] = nameof(BusinessCard.Mobile),
                ["email"] = nameof(BusinessCard.Email),
                ["website"] = nameof(BusinessCard.Website)
            };

        private static readonly HashSet<string> PhoneFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "tel", "extension", "fax", "mobile"
        };

        public IReadOnlyList<DuplicateMatchResult> FindMatches(
            BusinessCard candidate,
            IEnumerable<BusinessCard> existingCards,
            DuplicateComparisonSettings settings)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(existingCards);

            var fields = NormalizeFields(settings?.Fields);
            if (fields.Count == 0)
            {
                fields.Add("email");
            }

            var matchOperator = settings?.MatchOperator ?? DuplicateMatchOperator.Or;
            var results = new List<DuplicateMatchResult>();

            foreach (var existingCard in existingCards)
            {
                if (existingCard == null || existingCard.Id == candidate.Id)
                {
                    continue;
                }

                var matchedFields = new List<string>();
                var allFieldsMatch = true;

                foreach (var field in fields)
                {
                    var candidateValue = GetNormalizedValue(candidate, field);
                    var existingValue = GetNormalizedValue(existingCard, field);
                    var matches = candidateValue.Length > 0
                        && existingValue.Length > 0
                        && string.Equals(candidateValue, existingValue, StringComparison.Ordinal);

                    if (matches)
                    {
                        matchedFields.Add(field);
                    }
                    else
                    {
                        allFieldsMatch = false;
                    }
                }

                var isMatch = matchOperator == DuplicateMatchOperator.And
                    ? allFieldsMatch && matchedFields.Count == fields.Count
                    : matchedFields.Count > 0;

                if (isMatch)
                {
                    results.Add(new DuplicateMatchResult
                    {
                        ExistingCard = existingCard,
                        MatchedFields = matchedFields
                    });
                }
            }

            return results;
        }

        public bool IsSupportedField(string fieldKey) =>
            !string.IsNullOrWhiteSpace(fieldKey)
            && DuplicateComparisonSettings.SupportedFieldKeys.Contains(fieldKey)
            && PropertyNames.ContainsKey(fieldKey);

        private static List<string> NormalizeFields(IEnumerable<string>? fields)
        {
            return fields?
                .Where(field => !string.IsNullOrWhiteSpace(field)
                    && DuplicateComparisonSettings.SupportedFieldKeys.Contains(field)
                    && PropertyNames.ContainsKey(field))
                .Select(field => field.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static string GetNormalizedValue(BusinessCard card, string field)
        {
            if (!PropertyNames.TryGetValue(field, out var propertyName))
            {
                return string.Empty;
            }

            var value = BusinessCardFieldAccessor.GetTextValue(card, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
            normalized = Regex.Replace(normalized, @"\s+", " ");

            if (PhoneFields.Contains(field))
            {
                normalized = Regex.Replace(normalized, @"[\s\-\(\)]+", string.Empty);
            }

            return normalized.ToLower(CultureInfo.InvariantCulture);
        }
    }
}

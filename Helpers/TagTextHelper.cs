using System;
using System.Collections.Generic;
using System.Linq;

namespace PlustekBCR.Helpers
{
    public static class TagTextHelper
    {
        public static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        public static IReadOnlyList<string> Split(string? rawTags)
        {
            return (rawTags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool ContainsIgnoreCase(IEnumerable<string> values, string? candidate)
        {
            var normalized = Normalize(candidate);
            return !string.IsNullOrEmpty(normalized)
                && values.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static bool AddIfMissing(ICollection<string> values, string? candidate)
        {
            var normalized = Normalize(candidate);
            if (string.IsNullOrEmpty(normalized) || ContainsIgnoreCase(values, normalized))
            {
                return false;
            }

            values.Add(normalized);
            return true;
        }

        public static bool RemoveFirstIgnoreCase(ICollection<string> values, string? candidate)
        {
            var normalized = Normalize(candidate);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var existing = values.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                return false;
            }

            return values.Remove(existing);
        }

        public static string Join(IEnumerable<string> values)
        {
            return string.Join(", ",
                values.Select(Normalize)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}

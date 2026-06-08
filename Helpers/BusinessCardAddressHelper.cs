using System.Collections.Generic;
using System.Linq;

namespace PlustekBCR.Helpers
{
    public static class BusinessCardAddressHelper
    {
        public static string ComposeFullName(PlustekBCR.Models.MarketCode marketCode, string? firstName, string? middleName, string? lastName, string? suffix)
        {
            IEnumerable<string?> parts = marketCode == PlustekBCR.Models.MarketCode.JP
                ? new[] { lastName, firstName }
                : new[] { firstName, middleName, lastName, suffix };

            parts = parts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim());

            return string.Join(" ", parts);
        }

        public static string ComposeDepartmentFull(string? department1, string? department2, string? department3, string? department4)
        {
            var parts = new[] { department1, department2, department3, department4 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim());

            return string.Join(" / ", parts);
        }

        public static string ComposeFullAddress(string? addressLine1, string? addressLine2, string? city, string? state, string? zipCode, string? country)
        {
            var parts = new List<string>();

            AddIfPresent(parts, addressLine1);
            AddIfPresent(parts, addressLine2);
            AddIfPresent(parts, city);
            AddIfPresent(parts, state);
            AddIfPresent(parts, zipCode);
            AddIfPresent(parts, country);

            return string.Join(", ", parts);
        }

        private static void AddIfPresent(List<string> parts, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value.Trim());
            }
        }
    }
}

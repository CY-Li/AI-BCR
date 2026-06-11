using System.Collections.Generic;
using System.Linq;
using PlustekBCR.Models;

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

        public static string ComposeFullAddress(MarketCode marketCode, string? addressLine1, string? addressLine2, string? city, string? state, string? zipCode, string? country)
        {
            if (marketCode == MarketCode.US)
            {
                var parts = new List<string>();
                AddIfPresent(parts, addressLine1);
                AddIfPresent(parts, addressLine2);

                var cityStateZip = new List<string>();
                AddIfPresent(cityStateZip, city);
                AddIfPresent(cityStateZip, state);

                var locality = string.Join(", ", cityStateZip);
                if (!string.IsNullOrWhiteSpace(zipCode))
                {
                    locality = string.IsNullOrWhiteSpace(locality)
                        ? zipCode.Trim()
                        : $"{locality} {zipCode.Trim()}";
                }

                if (!string.IsNullOrWhiteSpace(locality))
                {
                    parts.Add(locality);
                }

                AddIfPresent(parts, country);
                return string.Join(", ", parts);
            }

            var jpParts = new List<string>();
            AddIfPresent(jpParts, addressLine1);
            AddIfPresent(jpParts, addressLine2);
            AddIfPresent(jpParts, city);
            AddIfPresent(jpParts, state);
            AddIfPresent(jpParts, zipCode);
            AddIfPresent(jpParts, country);
            return string.Join(", ", jpParts);
        }

        public static string ComposeFullAddress(string? addressLine1, string? addressLine2, string? city, string? state, string? zipCode, string? country)
        {
            return ComposeFullAddress(MarketCode.JP, addressLine1, addressLine2, city, state, zipCode, country);
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

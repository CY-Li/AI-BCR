using System;
using System.Linq;
using System.Reflection;
using PlustekBCR.Models;

namespace PlustekBCR.Helpers
{
    public static class BusinessCardFieldAccessor
    {
        public static string GetTextValue(BusinessCard card, string propertyName)
        {
            var property = typeof(BusinessCard).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return string.Empty;
            }

            var value = property.GetValue(card);
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Enum enumValue => enumValue.ToString(),
                byte[] => string.Empty,
                System.Collections.IEnumerable enumerable when property.PropertyType != typeof(string) => string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }

        public static void SetTextValue(BusinessCard card, string propertyName, string? rawValue)
        {
            var property = typeof(BusinessCard).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            var value = rawValue?.Trim() ?? string.Empty;

            if (property.PropertyType == typeof(string))
            {
                property.SetValue(card, value);
                return;
            }

            if (property.PropertyType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out var dateTime))
                {
                    property.SetValue(card, dateTime);
                }
                return;
            }

            if (property.PropertyType.IsEnum)
            {
                try
                {
                    var parsed = Enum.Parse(property.PropertyType, value, true);
                    property.SetValue(card, parsed);
                }
                catch
                {
                }
            }
        }

        public static void SetIdentityFromFullName(BusinessCard card)
        {
            if (string.IsNullOrWhiteSpace(card.FullName))
            {
                return;
            }

            var parts = card.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(card.FirstName))
            {
                card.FirstName = parts.First();
            }

            if (string.IsNullOrWhiteSpace(card.LastName) && parts.Length > 1)
            {
                card.LastName = parts.Last();
            }

            if (string.IsNullOrWhiteSpace(card.MiddleName) && parts.Length > 2)
            {
                card.MiddleName = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
            }
        }
    }
}

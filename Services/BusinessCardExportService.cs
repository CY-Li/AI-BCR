using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Helpers;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public sealed class BusinessCardExportService : IBusinessCardExportService
    {
        private static readonly string[] GoogleCsvHeaders =
        {
            "First Name", "Middle Name", "Last Name", "Name Suffix",
            "Phonetic First Name", "Phonetic Last Name",
            "Organization Name", "Organization Title", "Organization Department",
            "Notes", "Labels",
            // Google Contacts still requires the legacy E-mail/Type spelling in some importers.
            // Using Email/Label causes both columns to be imported as custom fields.
            "E-mail 1 - Type", "E-mail 1 - Value",
            "Phone 1 - Label", "Phone 1 - Value",
            "Phone 2 - Label", "Phone 2 - Value",
            "Phone 3 - Label", "Phone 3 - Value",
            "Address 1 - Label", "Address 1 - Country", "Address 1 - Street",
            "Address 1 - Extended Address", "Address 1 - City", "Address 1 - Region",
            "Address 1 - Postal Code", "Website 1 - Label", "Website 1 - Value"
        };

        private static readonly string[] OutlookCsvHeaders =
        {
            "First Name", "Middle Name", "Last Name", "Suffix", "Company", "Department",
            "Job Title", "Business Phone", "Mobile Phone", "Business Fax", "E-mail Address",
            "Business Street", "Business Street 2", "Business City", "Business State",
            "Business Postal Code", "Business Country/Region", "Web Page", "Notes", "Categories"
        };

        private readonly IBusinessCardFieldService _fieldService;

        public BusinessCardExportService(IBusinessCardFieldService fieldService)
        {
            _fieldService = fieldService;
        }

        public async Task<int> ExportCsvAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            await ExportAsync(cards, destinationPath, ContactExportFormat.Csv, cancellationToken);

        public async Task<int> ExportAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            ContactExportFormat format,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cards);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            var fullPath = Path.GetFullPath(destinationPath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The export destination has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: format != ContactExportFormat.VCard)))
                {
                    switch (format)
                    {
                        case ContactExportFormat.Csv:
                            await WriteStandardCsvAsync(writer, cards, cancellationToken);
                            break;
                        case ContactExportFormat.GoogleCsv:
                            await WriteCsvAsync(writer, GoogleCsvHeaders, cards, GetGoogleCsvRow, cancellationToken);
                            break;
                        case ContactExportFormat.OutlookCsv:
                            await WriteCsvAsync(writer, OutlookCsvHeaders, cards, GetOutlookCsvRow, cancellationToken);
                            break;
                        case ContactExportFormat.VCard:
                            await WriteVCardsAsync(writer, cards, cancellationToken);
                            break;
                        case ContactExportFormat.PlainText:
                            await WritePlainTextAsync(writer, cards, cancellationToken);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(format), format, null);
                    }

                    await writer.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, fullPath, overwrite: true);
                return cards.Count;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private async Task WriteStandardCsvAsync(
            TextWriter writer,
            IReadOnlyList<BusinessCard> cards,
            CancellationToken cancellationToken)
        {
            var fields = _fieldService.GetFields(BusinessCardSurface.Export).ToArray();
            await WriteCsvAsync(
                writer,
                fields.Select(field => field.Key),
                cards,
                card => fields.Select(field => BusinessCardFieldAccessor.GetTextValue(card, field.PropertyName)),
                cancellationToken);
        }

        private static async Task WriteCsvAsync(
            TextWriter writer,
            IEnumerable<string> headers,
            IReadOnlyList<BusinessCard> cards,
            Func<BusinessCard, IEnumerable<string?>> rowFactory,
            CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsv)));
            foreach (var card in cards)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(",", rowFactory(card).Select(EscapeCsv)));
            }
        }

        private static IEnumerable<string?> GetGoogleCsvRow(BusinessCard card)
        {
            return new string?[]
            {
                GetExportFirstName(card),
                card.MiddleName,
                card.LastName,
                card.Suffix,
                card.FirstNameKana,
                card.LastNameKana,
                card.CompanyName,
                card.JobTitle,
                card.DepartmentFull,
                BuildNotes(card, includePhoneticName: false),
                card.Tag,
                "Work",
                card.Email,
                "Work",
                card.Tel,
                "Mobile",
                card.Mobile,
                "Work Fax",
                card.Fax,
                "Work",
                card.Country,
                FirstNonEmpty(card.AddressLine1, card.FullAddress),
                card.AddressLine2,
                card.City,
                card.State,
                card.ZipCode,
                "Work",
                card.Website
            };
        }

        private static IEnumerable<string?> GetOutlookCsvRow(BusinessCard card)
        {
            return new string?[]
            {
                GetExportFirstName(card),
                card.MiddleName,
                card.LastName,
                card.Suffix,
                card.CompanyName,
                card.DepartmentFull,
                card.JobTitle,
                card.Tel,
                card.Mobile,
                card.Fax,
                card.Email,
                FirstNonEmpty(card.AddressLine1, card.FullAddress),
                card.AddressLine2,
                card.City,
                card.State,
                card.ZipCode,
                card.Country,
                card.Website,
                BuildNotes(card, includePhoneticName: true),
                card.Tag
            };
        }

        private static async Task WriteVCardsAsync(
            TextWriter writer,
            IReadOnlyList<BusinessCard> cards,
            CancellationToken cancellationToken)
        {
            foreach (var card in cards)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lines = new List<string>
                {
                    "BEGIN:VCARD",
                    "VERSION:3.0",
                    $"N:{EscapeVCard(card.LastName)};{EscapeVCard(GetExportFirstName(card))};{EscapeVCard(card.MiddleName)};;{EscapeVCard(card.Suffix)}",
                    $"FN:{EscapeVCard(card.DisplayName)}"
                };

                AddVCardLine(lines, "ORG", JoinVCardComponents(card.CompanyName, card.Department1, card.Department2, card.Department3, card.Department4));
                AddVCardLine(lines, "TITLE", EscapeVCard(card.JobTitle));
                AddVCardLine(lines, "TEL;TYPE=WORK,VOICE", EscapeVCard(card.Tel));
                AddVCardLine(lines, "TEL;TYPE=CELL", EscapeVCard(card.Mobile));
                AddVCardLine(lines, "TEL;TYPE=WORK,FAX", EscapeVCard(card.Fax));
                AddVCardLine(lines, "EMAIL;TYPE=INTERNET,WORK", EscapeVCard(card.Email));

                var street = FirstNonEmpty(card.AddressLine1, card.FullAddress);
                if (HasAnyValue(street, card.AddressLine2, card.City, card.State, card.ZipCode, card.Country))
                {
                    lines.Add($"ADR;TYPE=WORK:;{EscapeVCard(card.AddressLine2)};{EscapeVCard(street)};{EscapeVCard(card.City)};{EscapeVCard(card.State)};{EscapeVCard(card.ZipCode)};{EscapeVCard(card.Country)}");
                }

                AddVCardLine(lines, "URL", EscapeVCard(card.Website));
                AddVCardLine(lines, "NOTE", EscapeVCard(BuildNotes(card, includePhoneticName: true)));
                AddVCardLine(lines, "CATEGORIES", EscapeVCard(card.Tag));
                lines.Add("END:VCARD");

                foreach (var line in lines)
                {
                    await writer.WriteAsync(FoldVCardLine(line));
                    await writer.WriteAsync("\r\n");
                }
            }
        }

        private static async Task WritePlainTextAsync(
            TextWriter writer,
            IReadOnlyList<BusinessCard> cards,
            CancellationToken cancellationToken)
        {
            for (var index = 0; index < cards.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var card = cards[index];
                await writer.WriteLineAsync($"Full Name: {card.FullName}");
                await writer.WriteLineAsync($"Job Title: {card.JobTitle}");
                await writer.WriteLineAsync($"Company: {card.CompanyName}");
                await writer.WriteLineAsync($"Telephone: {card.Tel}");
                await writer.WriteLineAsync($"Mobile: {card.Mobile}");
                await writer.WriteLineAsync($"Email: {card.Email}");
                await writer.WriteLineAsync($"Full Address: {card.FullAddress}");
                await writer.WriteLineAsync($"Country: {card.Country}");
                await writer.WriteLineAsync($"Website: {card.Website}");
                await writer.WriteLineAsync($"Tag: {card.Tag}");
                await writer.WriteLineAsync($"Scan Date: {card.ScanDate:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"Status: {card.Status}");

                if (index < cards.Count - 1)
                {
                    await writer.WriteLineAsync();
                }
            }
        }

        private static void AddVCardLine(ICollection<string> lines, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"{name}:{value}");
            }
        }

        private static string JoinVCardComponents(params string?[] values) =>
            string.Join(";", values
                .TakeWhile((_, index) => index == 0 || values.Skip(index).Any(value => !string.IsNullOrWhiteSpace(value)))
                .Select(EscapeVCard));

        private static string BuildNotes(BusinessCard card, bool includePhoneticName)
        {
            var parts = new List<string>();
            if (includePhoneticName && !string.IsNullOrWhiteSpace(card.FullNameKana))
            {
                parts.Add($"Phonetic name: {card.FullNameKana.Trim()}");
            }

            if (card.Notes != null)
            {
                parts.AddRange(card.Notes
                    .Where(note => !string.IsNullOrWhiteSpace(note.Content))
                    .OrderBy(note => note.CreatedAt)
                    .Select(note => note.Content.Trim()));
            }

            return string.Join(Environment.NewLine, parts);
        }

        private static string GetExportFirstName(BusinessCard card)
        {
            if (!string.IsNullOrWhiteSpace(card.FirstName))
            {
                return card.FirstName;
            }

            return string.IsNullOrWhiteSpace(card.LastName) ? card.FullName : string.Empty;
        }

        private static string FirstNonEmpty(string? preferred, string? fallback) =>
            !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback ?? string.Empty;

        private static bool HasAnyValue(params string?[] values) =>
            values.Any(value => !string.IsNullOrWhiteSpace(value));

        internal static string EscapeVCard(string? value) => (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n")
            .Replace(";", "\\;")
            .Replace(",", "\\,");

        internal static string FoldVCardLine(string line)
        {
            const int firstLineLimit = 75;
            const int continuationLimit = 74;
            if (Encoding.UTF8.GetByteCount(line) <= firstLineLimit)
            {
                return line;
            }

            var result = new StringBuilder();
            var index = 0;
            var limit = firstLineLimit;
            while (index < line.Length)
            {
                var start = index;
                var byteCount = 0;
                while (index < line.Length)
                {
                    var charCount = char.IsHighSurrogate(line[index]) && index + 1 < line.Length ? 2 : 1;
                    var nextByteCount = Encoding.UTF8.GetByteCount(line.AsSpan(index, charCount));
                    if (byteCount + nextByteCount > limit)
                    {
                        break;
                    }

                    byteCount += nextByteCount;
                    index += charCount;
                }

                if (result.Length > 0)
                {
                    result.Append("\r\n ");
                }

                result.Append(line.AsSpan(start, index - start));
                limit = continuationLimit;
            }

            return result.ToString();
        }

        internal static string EscapeCsv(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }

            return text;
        }
    }
}

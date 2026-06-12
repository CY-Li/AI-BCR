using System.Text.RegularExpressions;
using PlustekBCR.Models;
using PlustekBCR.Models.Ocr;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Parsing
{
    public class OcrResultParser : IOcrResultParser
    {
        private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WebsiteRegex = new(@"((https?://)?(www\.)?[A-Z0-9\-]+\.[A-Z]{2,}(/[^\s]*)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"(\+?\d[\d\s().\-]{7,}\d)", RegexOptions.Compiled);

        public RecognizedBusinessCardData Parse(OcrDocumentResult documentResult)
        {
            if (documentResult.StructuredData != null)
            {
                return documentResult.StructuredData;
            }

            return documentResult.Market == MarketCode.US
                ? ParseUsFallback(documentResult)
                : ParseJpFallback(documentResult);
        }

        private RecognizedBusinessCardData ParseJpFallback(OcrDocumentResult documentResult)
        {
            var lines = GetNormalizedLines(documentResult);
            var result = new RecognizedBusinessCardData();
            if (lines.Count == 0)
            {
                return result;
            }

            result.Email = FindFirst(lines, text => EmailRegex.IsMatch(text), text => EmailRegex.Match(text).Value);
            result.Website = FindFirst(lines, text => WebsiteRegex.IsMatch(text) && !EmailRegex.IsMatch(text), text => WebsiteRegex.Match(text).Value);
            result.Fax = FindPhone(lines, "fax", "f:");
            result.Mobile = FindPhone(lines, "mobile", "cell", "m:");
            result.Tel = FindPhone(lines, "tel", "phone", "office", "t:");
            if (string.IsNullOrWhiteSpace(result.Tel))
            {
                result.Tel = FindFirst(lines, text => PhoneRegex.IsMatch(text), text => PhoneRegex.Match(text).Value);
            }

            var nameCandidates = lines
                .Take(12)
                .Where(IsLikelyJpName)
                .OrderBy(text => text.Length)
                .ToList();
            if (nameCandidates.Count > 0)
            {
                result.FullName = nameCandidates[0];
            }

            result.JobTitle = FindFirst(lines, IsLikelyJpTitle, text => text);

            var companyCandidates = lines
                .Take(10)
                .Where(text => !string.Equals(text, result.FullName, StringComparison.OrdinalIgnoreCase))
                .Where(text => !string.Equals(text, result.JobTitle, StringComparison.OrdinalIgnoreCase))
                .Where(text => !EmailRegex.IsMatch(text) && !WebsiteRegex.IsMatch(text) && !PhoneRegex.IsMatch(text))
                .Where(IsLikelyJpCompany)
                .OrderByDescending(text => ScoreCompany(text))
                .ToList();
            if (companyCandidates.Count > 0)
            {
                result.CompanyName = companyCandidates[0];
            }

            PopulateAddressData(lines, result);
            return result;
        }

        private RecognizedBusinessCardData ParseUsFallback(OcrDocumentResult documentResult)
        {
            var lines = GetNormalizedLines(documentResult);
            var result = new RecognizedBusinessCardData();
            if (lines.Count == 0)
            {
                return result;
            }

            result.Email = FindFirst(lines, text => EmailRegex.IsMatch(text), text => EmailRegex.Match(text).Value);
            result.Website = FindFirst(lines, text => WebsiteRegex.IsMatch(text) && !EmailRegex.IsMatch(text), text => WebsiteRegex.Match(text).Value);
            result.Fax = FindPhone(lines, "fax");
            result.Mobile = FindPhone(lines, "mobile", "cell", "m:");
            result.Tel = FindPhone(lines, "tel", "phone", "office", "t:", "direct");
            if (string.IsNullOrWhiteSpace(result.Tel))
            {
                result.Tel = FindFirst(lines, text => PhoneRegex.IsMatch(text), text => PhoneRegex.Match(text).Value);
            }

            result.Extension = FindFirst(lines,
                text => text.Contains("ext", StringComparison.OrdinalIgnoreCase) || text.Contains("extension", StringComparison.OrdinalIgnoreCase),
                ExtractExtension);

            var nameCandidates = lines
                .Take(12)
                .Where(IsLikelyUsName)
                .OrderBy(text => text.Length)
                .ToList();
            if (nameCandidates.Count > 0)
            {
                result.FullName = nameCandidates[0];
            }

            result.JobTitle = FindFirst(lines, IsLikelyUsTitle, text => text);

            var companyCandidates = lines
                .Take(10)
                .Where(text => !string.Equals(text, result.FullName, StringComparison.OrdinalIgnoreCase))
                .Where(text => !string.Equals(text, result.JobTitle, StringComparison.OrdinalIgnoreCase))
                .Where(text => !EmailRegex.IsMatch(text) && !WebsiteRegex.IsMatch(text) && !PhoneRegex.IsMatch(text))
                .Where(IsLikelyUsCompany)
                .OrderByDescending(text => ScoreCompany(text))
                .ToList();
            if (companyCandidates.Count > 0)
            {
                result.CompanyName = companyCandidates[0];
            }

            PopulateAddressData(lines, result);
            return result;
        }

        private static List<string> GetNormalizedLines(OcrDocumentResult documentResult)
        {
            return documentResult.Pages
                .OrderBy(page => page.Page)
                .SelectMany(page => page.Blocks)
                .Where(block => !string.IsNullOrWhiteSpace(block.Text))
                .OrderBy(block => block.Page)
                .ThenBy(block => block.Location.Length > 1 ? block.Location[1] : int.MaxValue)
                .ThenBy(block => block.Location.Length > 0 ? block.Location[0] : int.MaxValue)
                .Select(block => Normalize(block.Text))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        private static void PopulateAddressData(List<string> lines, RecognizedBusinessCardData result)
        {
            var addressLines = lines
                .Where(IsLikelyAddress)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();
            if (addressLines.Count > 0)
            {
                result.AddressLine1 = addressLines[0];
            }

            if (addressLines.Count > 1)
            {
                result.AddressLine2 = addressLines[1];
            }

            result.ZipCode = FindFirst(lines, text => text.Any(char.IsDigit) && (text.Contains("ZIP", StringComparison.OrdinalIgnoreCase) || text.Count(char.IsDigit) >= 5), ExtractZipCode);
            result.Country = FindFirst(lines, text => text.Contains("Japan", StringComparison.OrdinalIgnoreCase) || text.Contains("United States", StringComparison.OrdinalIgnoreCase), text => text);
            result.State = FindFirst(lines, text => text.Contains(",") && text.Count(char.IsLetter) >= 2 && text.Count(char.IsDigit) <= 8, ExtractState);
            result.City = FindFirst(lines, text => text.Contains(",") && text.Count(char.IsLetter) >= 2 && text.Count(char.IsDigit) <= 8, ExtractCity);
            result.FullAddress = string.Join(" ", new[] { result.AddressLine1, result.AddressLine2, result.City, result.State, result.ZipCode, result.Country }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string Normalize(string text)
        {
            return text.Replace('℧', ' ').Trim();
        }

        private static string FindFirst(IEnumerable<string> lines, Func<string, bool> predicate, Func<string, string> selector)
        {
            foreach (var line in lines)
            {
                if (predicate(line))
                {
                    return selector(line).Trim();
                }
            }

            return string.Empty;
        }

        private static string FindPhone(IEnumerable<string> lines, params string[] keywords)
        {
            foreach (var line in lines)
            {
                var normalized = line.ToLowerInvariant();
                if (keywords.Any(keyword => normalized.Contains(keyword)) && PhoneRegex.IsMatch(line))
                {
                    return PhoneRegex.Match(line).Value.Trim();
                }
            }

            return string.Empty;
        }

        private static bool IsLikelyJpName(string text)
        {
            if (text.Length < 2 || text.Length > 40)
            {
                return false;
            }

            if (EmailRegex.IsMatch(text) || WebsiteRegex.IsMatch(text) || PhoneRegex.IsMatch(text))
            {
                return false;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length is >= 1 and <= 4;
        }

        private static bool IsLikelyUsName(string text)
        {
            if (text.Length < 4 || text.Length > 40)
            {
                return false;
            }

            if (EmailRegex.IsMatch(text) || WebsiteRegex.IsMatch(text) || PhoneRegex.IsMatch(text))
            {
                return false;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length is >= 2 and <= 4
                && words.All(word => char.IsLetter(word[0]));
        }

        private static bool IsLikelyJpTitle(string text)
        {
            var keywords = new[]
            {
                "部長", "課長", "主任", "担当", "支店", "営業", "取締役", "マネージャ", "manager", "director", "engineer"
            };

            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLikelyUsTitle(string text)
        {
            var keywords = new[]
            {
                "manager", "director", "engineer", "sales", "marketing", "officer", "president", "chief", "consultant", "developer", "specialist"
            };

            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLikelyJpCompany(string text)
        {
            var keywords = new[] { "株式会社", "有限会社", "company", "group", "corp", "co.", "inc" };
            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || text.Length >= 6;
        }

        private static bool IsLikelyUsCompany(string text)
        {
            var keywords = new[] { "inc", "co", "corp", "company", "limited", "ltd", "group", "solutions", "systems", "tech" };
            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || text.Length >= 6;
        }

        private static int ScoreCompany(string text)
        {
            var score = text.Length;
            if (text.Contains("inc", StringComparison.OrdinalIgnoreCase) || text.Contains("corp", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }

            return score;
        }

        private static bool IsLikelyAddress(string text)
        {
            if (EmailRegex.IsMatch(text) || WebsiteRegex.IsMatch(text))
            {
                return false;
            }

            return text.Any(char.IsDigit)
                && (text.Contains("road", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("rd", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("street", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("st", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("ave", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("lane", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("city", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("california", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("tokyo", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("taipei", StringComparison.OrdinalIgnoreCase));
        }

        private static string ExtractZipCode(string text)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits.Length >= 5 ? digits[..Math.Min(digits.Length, 7)] : string.Empty;
        }

        private static string ExtractExtension(string text)
        {
            var match = Regex.Match(text, @"(?:ext|extension)\s*[:.]?\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ExtractState(string text)
        {
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2 ? parts[^2] : string.Empty;
        }

        private static string ExtractCity(string text)
        {
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }
    }
}

using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Windows.System;

namespace PlustekBCR.Services
{
    public static class ContactActionUriBuilder
    {
        public static bool TryCreateEmailUri(string? email, out Uri? uri)
        {
            uri = null;
            var value = email?.Trim();
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains('\r')
                || value.Contains('\n')
                || !value.Contains('@'))
            {
                return false;
            }

            if (!MailAddress.TryCreate(value, out var address)
                || !string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Uri.TryCreate($"mailto:{address.Address}", UriKind.Absolute, out uri);
        }

        public static bool TryCreatePhoneUri(string? phoneNumber, out Uri? uri)
        {
            uri = null;
            var value = phoneNumber?.Trim();
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains('\r')
                || value.Contains('\n')
                || !value.Any(char.IsDigit))
            {
                return false;
            }

            return Uri.TryCreate($"tel:{Uri.EscapeDataString(value)}", UriKind.Absolute, out uri);
        }

        public static bool TryCreateWebsiteUri(string? website, out Uri? uri)
        {
            uri = null;
            var value = website?.Trim();
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains('\r')
                || value.Contains('\n'))
            {
                return false;
            }

            if (!value.Contains("://", StringComparison.Ordinal))
            {
                value = $"https://{value}";
            }

            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host);
        }
    }

    public class ContactActionService : IContactActionService
    {
        public Task<ContactActionResult> OpenEmailAsync(string? email) =>
            LaunchAsync(ContactActionUriBuilder.TryCreateEmailUri(email, out var uri), uri);

        public Task<ContactActionResult> OpenPhoneAsync(string? phoneNumber) =>
            LaunchAsync(ContactActionUriBuilder.TryCreatePhoneUri(phoneNumber, out var uri), uri);

        public Task<ContactActionResult> OpenWebsiteAsync(string? website) =>
            LaunchAsync(ContactActionUriBuilder.TryCreateWebsiteUri(website, out var uri), uri);

        private static async Task<ContactActionResult> LaunchAsync(bool isValid, Uri? uri)
        {
            if (!isValid || uri == null)
            {
                return ContactActionResult.InvalidValue;
            }

            try
            {
                return await Launcher.LaunchUriAsync(uri)
                    ? ContactActionResult.Success
                    : ContactActionResult.LaunchFailed;
            }
            catch
            {
                return ContactActionResult.LaunchFailed;
            }
        }
    }
}

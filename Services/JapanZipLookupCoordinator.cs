#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Helpers;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public sealed class JapanZipLookupCoordinator
    {
        public static string LookingUpMessage => App.GetService<ILocalizationService>().GetString("ZipLookup.LookingUp");
        public static string NotFoundMessage => App.GetService<ILocalizationService>().GetString("ZipLookup.NotFound");
        public static string UpdatedMessage => App.GetService<ILocalizationService>().GetString("ZipLookup.Updated");
        public static string FailedMessage => App.GetService<ILocalizationService>().GetString("ZipLookup.Failed");

        public static string NormalizeZip(string? zipCode)
        {
            return (zipCode ?? string.Empty).Replace("-", string.Empty).Trim();
        }

        public static bool IsLookupReady(string? zipCode)
        {
            return NormalizeZip(zipCode).Length == 7;
        }

        public async Task<JapanZipLookupOutcome> LookupAndApplyAsync(
            IZipCodeLookupService zipCodeLookupService,
            BusinessCard card,
            CancellationToken cancellationToken = default)
        {
            var normalizedZip = NormalizeZip(card.ZipCode);
            if (normalizedZip.Length != 7)
            {
                return JapanZipLookupOutcome.InvalidZip();
            }

            try
            {
                var result = await zipCodeLookupService.LookupJapanAddressAsync(normalizedZip, cancellationToken);
                if (result == null)
                {
                    return JapanZipLookupOutcome.NotFound(normalizedZip);
                }

                card.MarketCode = MarketCode.JP;
                card.ZipCode = result.Zipcode ?? normalizedZip;
                card.AddressLine1 = string.Concat(result.Address1 ?? string.Empty, result.Address2 ?? string.Empty, result.Address3 ?? string.Empty);
                card.FullAddress = BusinessCardAddressHelper.ComposeFullAddress(
                    card.MarketCode,
                    card.AddressLine1,
                    card.AddressLine2,
                    card.City,
                    card.State,
                    card.ZipCode,
                    card.Country);

                return JapanZipLookupOutcome.Updated(card.ZipCode);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return JapanZipLookupOutcome.Failed(normalizedZip);
            }
        }
    }

    public sealed class JapanZipLookupOutcome
    {
        private JapanZipLookupOutcome(bool isValidZip, bool isUpdated, string statusMessage)
        {
            IsValidZip = isValidZip;
            IsUpdated = isUpdated;
            StatusMessage = statusMessage;
        }

        public bool IsValidZip { get; }
        public bool IsUpdated { get; }
        public string StatusMessage { get; }

        public static JapanZipLookupOutcome InvalidZip() => new(false, false, string.Empty);
        public static JapanZipLookupOutcome NotFound(string normalizedZip) => new(normalizedZip.Length == 7, false, JapanZipLookupCoordinator.NotFoundMessage);
        public static JapanZipLookupOutcome Updated(string normalizedZip) => new(normalizedZip.Length == 7, true, JapanZipLookupCoordinator.UpdatedMessage);
        public static JapanZipLookupOutcome Failed(string normalizedZip) => new(normalizedZip.Length == 7, false, JapanZipLookupCoordinator.FailedMessage);
    }
}

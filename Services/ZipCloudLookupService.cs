using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public class ZipCloudLookupService : IZipCodeLookupService
    {
        private static readonly Uri BaseUri = new("https://zipcloud.ibsnet.co.jp/api/search");
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        public async Task<ZipCloudResult?> LookupJapanAddressAsync(string zipCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(zipCode))
            {
                return null;
            }

            var normalizedZip = zipCode.Replace("-", string.Empty).Trim();
            if (normalizedZip.Length != 7)
            {
                return null;
            }

            var requestUri = new Uri($"{BaseUri}?zipcode={normalizedZip}");
            using var response = await HttpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<ZipCloudResponse>(stream, cancellationToken: cancellationToken);
            return payload?.Results != null && payload.Results.Length > 0 ? payload.Results[0] : null;
        }
    }
}

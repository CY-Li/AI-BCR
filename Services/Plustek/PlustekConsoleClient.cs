using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PlustekBCR.Models;
using PlustekBCR.Models.Plustek;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Plustek
{
    public class PlustekConsoleClient : IPlustekConsoleClient
    {
        private readonly HttpClient _httpClient;
        private readonly IPlustekOptionsProvider _optionsProvider;
        private readonly Recognition.IRecognitionDiagnosticsService _diagnosticsService;
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<MarketCode, TokenCacheEntry> _tokenCache = new();
        private const int MaxTransientRetries = 2;

        public PlustekConsoleClient(
            HttpClient httpClient,
            IPlustekOptionsProvider optionsProvider,
            Recognition.IRecognitionDiagnosticsService diagnosticsService)
        {
            _httpClient = httpClient;
            _optionsProvider = optionsProvider;
            _diagnosticsService = diagnosticsService;
        }

        public async Task<string> IssueProjectTokenAsync(MarketCode market, CancellationToken cancellationToken = default)
        {
            if (_tokenCache.TryGetValue(market, out var cacheEntry)
                && !string.IsNullOrWhiteSpace(cacheEntry.AccessToken)
                && DateTimeOffset.UtcNow - cacheEntry.IssuedAt < TimeSpan.FromMinutes(15))
            {
                return cacheEntry.AccessToken;
            }

            var options = GetValidatedOptions(market);

            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Post, BuildUri(options, "projects/token"))
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = options.ApiKey,
                        ["client_secret"] = options.ApiSecret
                    })
                },
                "IssueProjectToken",
                market,
                authorizeWithToken: false,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "IssueProjectToken", market, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<ProjectTokenResponse>(_jsonSerializerOptions, cancellationToken);
            var token = body?.Data?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Project token response does not contain access token.");
            }

            _tokenCache[market] = new TokenCacheEntry(token, DateTimeOffset.UtcNow);
            return token;
        }

        public async Task<UploadUrlItem> CreateUploadUrlAsync(MarketCode market, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var options = GetValidatedOptions(market);
            using var response = await SendWithRetryAsync(
                token => CreateAuthorizedJsonRequest(HttpMethod.Post, options, "files/upload-url", token, $$"""
                {
                  "files": [
                    {
                      "file_name": "{{fileName}}",
                      "content_type": "{{contentType}}"
                    }
                  ]
                }
                """),
                "CreateUploadUrl",
                market,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "CreateUploadUrl", market, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<CreateUploadUrlResponse>(_jsonSerializerOptions, cancellationToken);
            var uploadUrl = body?.Data?.Urls?.FirstOrDefault();
            if (uploadUrl == null || string.IsNullOrWhiteSpace(uploadUrl.FileId) || string.IsNullOrWhiteSpace(uploadUrl.SignedUrl))
            {
                throw new InvalidOperationException("Upload URL response is missing upload information.");
            }

            return uploadUrl;
        }

        public async Task UploadFileAsync(string signedUrl, byte[] imageBytes, string contentType, CancellationToken cancellationToken = default)
        {
            using var response = await SendWithRetryAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Put, signedUrl)
                    {
                        Content = new ByteArrayContent(imageBytes)
                    };
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    return request;
                },
                "UploadFile",
                market: null,
                authorizeWithToken: false,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "UploadFile", null, cancellationToken);
        }

        public async Task ConfirmUploadAsync(MarketCode market, string fileId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var options = GetValidatedOptions(market);
            using var response = await SendWithRetryAsync(
                token => CreateAuthorizedJsonRequest(HttpMethod.Post, options, "files/confirm-upload", token, $$"""
                {
                  "files": [
                    {
                      "file_id": "{{fileId}}",
                      "file_name": "{{fileName}}",
                      "content_type": "{{contentType}}"
                    }
                  ]
                }
                """),
                "ConfirmUpload",
                market,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "ConfirmUpload", market, cancellationToken);
        }

        public async Task<string> CreateDocumentAsync(MarketCode market, string fileId, CancellationToken cancellationToken = default)
        {
            var options = GetValidatedOptions(market);
            using var response = await SendWithRetryAsync(
                token => CreateAuthorizedJsonRequest(HttpMethod.Post, options, "documents", token, $$"""
                {
                  "files": [
                    "{{fileId}}"
                  ]
                }
                """),
                "CreateDocument",
                market,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "CreateDocument", market, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<CreateDocumentResponse>(_jsonSerializerOptions, cancellationToken);
            var documentId = body?.Data?.Id;
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new InvalidOperationException("Document response is missing document id.");
            }

            return documentId;
        }

        public async Task<string> CreateEscanJobAsync(MarketCode market, string documentId, CancellationToken cancellationToken = default)
        {
            var options = GetValidatedOptions(market);
            using var response = await SendWithRetryAsync(
                token => CreateAuthorizedJsonRequest(HttpMethod.Post, options, "jobs", token, $$"""
                {
                  "service_id": "{{options.EscanServiceId}}",
                  "payload": {
                    "document_id": "{{documentId}}",
                    "parameters": {}
                  }
                }
                """),
                "CreateEscanJob",
                market,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "CreateEscanJob", market, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<CreateJobResponse>(_jsonSerializerOptions, cancellationToken);
            var jobId = body?.Data?.JobId;
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new InvalidOperationException("Create job response is missing job id.");
            }

            return jobId;
        }

        public async Task<EscanJobResultResponse> GetEscanJobResultAsync(MarketCode market, string jobId, CancellationToken cancellationToken = default)
        {
            var options = GetValidatedOptions(market);
            using var response = await SendWithRetryAsync(
                token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(options, $"jobs/{jobId}/results"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    return request;
                },
                "GetEscanJobResult",
                market,
                cancellationToken: cancellationToken);
            await EnsureSuccessStatusCodeAsync(response, "GetEscanJobResult", market, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<EscanJobResultResponse>(_jsonSerializerOptions, cancellationToken);
            if (body == null)
            {
                throw new InvalidOperationException("Escan result response is empty.");
            }

            return body;
        }

        private HttpRequestMessage CreateAuthorizedJsonRequest(HttpMethod method, PlustekConsoleOptions options, string relativePath, string token, string json)
        {
            var request = new HttpRequestMessage(method, BuildUri(options, relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }

        private static Uri BuildUri(PlustekConsoleOptions options, string relativePath)
        {
            return new($"{options.BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}");
        }

        private PlustekConsoleOptions GetValidatedOptions(MarketCode market)
        {
            var options = _optionsProvider.Get(market);
            if (string.IsNullOrWhiteSpace(options.BaseUrl)
                || string.IsNullOrWhiteSpace(options.ApiKey)
                || string.IsNullOrWhiteSpace(options.ApiSecret)
                || string.IsNullOrWhiteSpace(options.EscanServiceId))
            {
                throw new RecognitionException(RecognitionFailureKind.Configuration, $"AI recognition is not configured for {market} market.");
            }

            return options;
        }

        private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, string stage, MarketCode? market, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                await _diagnosticsService.LogAsync("PlustekApi", $"{stage} succeeded. Market={FormatMarket(market)} Status={(int)response.StatusCode}.", cancellationToken);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            await _diagnosticsService.LogAsync("PlustekApi", $"{stage} failed. Market={FormatMarket(market)} Status={(int)response.StatusCode}. Body={body}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<string, HttpRequestMessage> authorizedRequestFactory,
            string stage,
            MarketCode market,
            CancellationToken cancellationToken)
        {
            return await SendWithRetryAsync(
                async () =>
                {
                    var token = await IssueProjectTokenAsync(market, cancellationToken);
                    return authorizedRequestFactory(token);
                },
                stage,
                market,
                allowTokenRefresh: true,
                cancellationToken: cancellationToken);
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<HttpRequestMessage> requestFactory,
            string stage,
            MarketCode? market,
            bool authorizeWithToken,
            CancellationToken cancellationToken)
        {
            return await SendWithRetryAsync(
                () => Task.FromResult(requestFactory()),
                stage,
                market,
                allowTokenRefresh: authorizeWithToken,
                cancellationToken: cancellationToken);
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<Task<HttpRequestMessage>> requestFactory,
            string stage,
            MarketCode? market,
            bool allowTokenRefresh,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            var refreshedToken = false;

            while (true)
            {
                attempt++;
                try
                {
                    using var request = await requestFactory();
                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (allowTokenRefresh
                        && response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        && !refreshedToken)
                    {
                        response.Dispose();
                        refreshedToken = true;
                        if (market.HasValue)
                        {
                            ClearCachedToken(market.Value);
                        }

                        await _diagnosticsService.LogAsync("ApiStage", $"{stage} received 401. Market={FormatMarket(market)} Refreshing token and retrying.", cancellationToken);
                        continue;
                    }

                    if (attempt <= MaxTransientRetries && IsTransientStatusCode(response.StatusCode))
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);
                        await _diagnosticsService.LogAsync("ApiStage", $"{stage} transient status {(int)response.StatusCode} on attempt {attempt}. Market={FormatMarket(market)} Body={body}", cancellationToken);
                        response.Dispose();
                        await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                        continue;
                    }

                    return response;
                }
                catch (HttpRequestException ex) when (attempt <= MaxTransientRetries)
                {
                    await _diagnosticsService.LogAsync("ApiStage", $"{stage} transient network error on attempt {attempt}. Market={FormatMarket(market)} Message={ex.Message}", cancellationToken);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt <= MaxTransientRetries)
                {
                    await _diagnosticsService.LogAsync("ApiStage", $"{stage} transient timeout on attempt {attempt}. Market={FormatMarket(market)} Message={ex.Message}", cancellationToken);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                }
                catch (Exception ex) when (ex is not RecognitionException)
                {
                    throw new RecognitionException(RecognitionFailureKind.Network, $"{stage} failed before receiving a stable response.", ex);
                }
            }
        }

        private void ClearCachedToken(MarketCode market)
        {
            _tokenCache.Remove(market);
        }

        private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
        {
            var numeric = (int)statusCode;
            return statusCode == System.Net.HttpStatusCode.RequestTimeout
                || statusCode == (System.Net.HttpStatusCode)429
                || numeric >= 500;
        }

        private static TimeSpan GetRetryDelay(int attempt)
        {
            return TimeSpan.FromMilliseconds(300 * attempt);
        }

        private static string FormatMarket(MarketCode? market)
        {
            return market?.ToString() ?? "N/A";
        }

        private readonly record struct TokenCacheEntry(string AccessToken, DateTimeOffset IssuedAt);
    }
}

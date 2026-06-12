using System.Text.Json;
using PlustekBCR.Models;
using PlustekBCR.Models.Ocr;
using PlustekBCR.Models.Plustek;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Plustek
{
    public class PlustekOcrCoordinator : IPlustekOcrCoordinator
    {
        private readonly IPlustekConsoleClient _plustekConsoleClient;
        private readonly IPlustekOptionsProvider _optionsProvider;
        private readonly Recognition.IRecognitionDiagnosticsService _diagnosticsService;

        public PlustekOcrCoordinator(
            IPlustekConsoleClient plustekConsoleClient,
            IPlustekOptionsProvider optionsProvider,
            Recognition.IRecognitionDiagnosticsService diagnosticsService)
        {
            _plustekConsoleClient = plustekConsoleClient;
            _optionsProvider = optionsProvider;
            _diagnosticsService = diagnosticsService;
        }

        public async Task<OcrDocumentResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken = default)
        {
            var options = _optionsProvider.Get(request.Market);

            await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} begin upload workflow. Market={request.Market}, File={request.FileName}.", cancellationToken);
            var upload = await _plustekConsoleClient.CreateUploadUrlAsync(request.Market, request.FileName, request.ContentType, cancellationToken);
            await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} upload url created. Market={request.Market}, FileId={upload.FileId}.", cancellationToken);
            await _plustekConsoleClient.UploadFileAsync(upload.SignedUrl, request.ImageBytes, request.ContentType, cancellationToken);
            await _plustekConsoleClient.ConfirmUploadAsync(request.Market, upload.FileId, request.FileName, request.ContentType, cancellationToken);

            var documentId = await _plustekConsoleClient.CreateDocumentAsync(request.Market, upload.FileId, cancellationToken);
            var jobId = await _plustekConsoleClient.CreateEscanJobAsync(request.Market, documentId, cancellationToken);
            await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} escan job created. Market={request.Market}, DocumentId={documentId}, JobId={jobId}.", cancellationToken);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(5, options.ResultTimeoutSeconds));
            var pollCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await _plustekConsoleClient.GetEscanJobResultAsync(request.Market, jobId, cancellationToken);
                pollCount++;
                var documentResult = TryParseDocumentResult(response.Data, request.Market);
                await _diagnosticsService.LogAsync(
                    "JobStatus",
                    $"Card={request.BusinessCardId} market={request.Market} poll {pollCount} status={documentResult.JobStatus}. Errors={documentResult.ServiceErrors.Count}.",
                    cancellationToken);

                if (IsFailedStatus(documentResult.JobStatus))
                {
                    var errorMessage = documentResult.ServiceErrors.Count > 0
                        ? string.Join(" | ", documentResult.ServiceErrors)
                        : "AI recognition failed at service side.";
                    await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} market={request.Market} job failed. Payload={BuildPayloadPreview(response.Data)}", cancellationToken);
                    throw new RecognitionException(RecognitionFailureKind.JobFailed, errorMessage);
                }

                if (documentResult.StructuredData != null || documentResult.Pages.Count > 0)
                {
                    if (documentResult.StructuredData != null)
                    {
                        await _diagnosticsService.LogAsync("StructuredParse", $"Card={request.BusinessCardId} market={request.Market} structured branch={(request.Market == MarketCode.US ? "US" : "JP")} selected.", cancellationToken);
                    }

                    await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} market={request.Market} escan result ready after {pollCount} polls.", cancellationToken);
                    return documentResult;
                }

                if (string.Equals(documentResult.JobStatus, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    await _diagnosticsService.LogAsync(
                        "StructuredParse",
                        $"Card={request.BusinessCardId} market={request.Market} completed with empty/unreadable result. Payload={BuildPayloadPreview(response.Data)}",
                        cancellationToken);
                    throw new RecognitionException(RecognitionFailureKind.UnreadableResult, "AI returned an unreadable result.");
                }

                if (pollCount <= 3 || pollCount % 10 == 0)
                {
                    await _diagnosticsService.LogAsync(
                        "PlustekCoordinator",
                        $"Card={request.BusinessCardId} market={request.Market} poll {pollCount} returned no parsable OCR pages. Payload={BuildPayloadPreview(response.Data)}",
                        cancellationToken);
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    await _diagnosticsService.LogAsync("PlustekCoordinator", $"Card={request.BusinessCardId} market={request.Market} escan result timed out after {pollCount} polls.", cancellationToken);
                    throw new RecognitionException(RecognitionFailureKind.TimedOut, "Timed out while waiting for escan result.");
                }

                await Task.Delay(Math.Max(250, options.PollIntervalMs), cancellationToken);
            }
        }

        private static OcrDocumentResult TryParseDocumentResult(JsonElement data, MarketCode market)
        {
            var result = new OcrDocumentResult
            {
                Market = market,
                JobStatus = GetString(data, "status")
            };
            result.ServiceErrors.AddRange(ExtractErrors(data));

            if (TryParseStructuredOutput(data, market, out var structuredData))
            {
                result.StructuredData = structuredData;
                return result;
            }

            if (TryCollectPages(data, result.Pages))
            {
                return result;
            }

            if (TryCollectPagesFromString(data, result.Pages))
            {
                return result;
            }

            return result;
        }

        private static bool TryCollectPages(JsonElement element, List<OcrPageResult> pages)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryParsePage(item, out var page))
                    {
                        pages.Add(page);
                    }
                    else
                    {
                        TryCollectPages(item, pages);
                    }
                }

                return pages.Count > 0;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryParsePage(element, out var directPage))
            {
                pages.Add(directPage);
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryCollectPages(property.Value, pages))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseStructuredOutput(JsonElement element, MarketCode market, out RecognizedBusinessCardData structuredData)
        {
            structuredData = new RecognizedBusinessCardData();

            if (!TryGetPropertyIgnoreCase(element, "status", out var statusElement)
                || !string.Equals(statusElement.GetString(), "completed", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(element, "output", out var outputElement)
                || outputElement.ValueKind != JsonValueKind.Object
                || !TryGetPropertyIgnoreCase(outputElement, "data", out var outputDataElement)
                || outputDataElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (market == MarketCode.US)
            {
                PopulateStructuredUsData(outputDataElement, structuredData);
            }
            else
            {
                PopulateStructuredJpData(outputDataElement, structuredData);
            }

            return HasStructuredContent(structuredData);
        }

        private static void PopulateStructuredJpData(JsonElement outputDataElement, RecognizedBusinessCardData structuredData)
        {
            PopulateSharedStructuredData(outputDataElement, structuredData);
            structuredData.FirstNameKana = GetString(outputDataElement, "first_name_kana");
            structuredData.LastNameKana = GetString(outputDataElement, "last_name_kana");
            structuredData.FullNameKana = GetString(outputDataElement, "full_name_kana");
        }

        private static void PopulateStructuredUsData(JsonElement outputDataElement, RecognizedBusinessCardData structuredData)
        {
            PopulateSharedStructuredData(outputDataElement, structuredData);
            structuredData.MiddleName = GetString(outputDataElement, "MiddleName");
            structuredData.Suffix = GetString(outputDataElement, "Suffix");
            structuredData.Extension = GetString(outputDataElement, "Extension");
        }

        private static void PopulateSharedStructuredData(JsonElement outputDataElement, RecognizedBusinessCardData structuredData)
        {
            structuredData.FullName = GetString(outputDataElement, "FullName");
            structuredData.FirstName = GetString(outputDataElement, "FirstName");
            structuredData.LastName = GetString(outputDataElement, "LastName");
            structuredData.CompanyName = GetString(outputDataElement, "CompanyName");
            structuredData.Department1 = GetString(outputDataElement, "Department1");
            structuredData.Department2 = GetString(outputDataElement, "Department2");
            structuredData.Department3 = GetString(outputDataElement, "Department3");
            structuredData.Department4 = GetString(outputDataElement, "Department4");
            structuredData.DepartmentFull = GetString(outputDataElement, "DepartmentFull");
            structuredData.JobTitle = GetString(outputDataElement, "JobTitle");
            structuredData.Email = GetString(outputDataElement, "Email");
            structuredData.Tel = GetString(outputDataElement, "Tel");
            structuredData.Mobile = GetString(outputDataElement, "Mobile");
            structuredData.Fax = GetString(outputDataElement, "Fax");
            structuredData.Website = GetString(outputDataElement, "Website");
            structuredData.AddressLine1 = GetString(outputDataElement, "AddressLine1");
            structuredData.AddressLine2 = GetString(outputDataElement, "AddressLine2");
            structuredData.City = GetString(outputDataElement, "City");
            structuredData.State = GetString(outputDataElement, "State");
            structuredData.ZipCode = GetString(outputDataElement, "ZipCode");
            structuredData.Country = GetString(outputDataElement, "Country");
            structuredData.FullAddress = GetString(outputDataElement, "FullAddress");
        }

        private static bool HasStructuredContent(RecognizedBusinessCardData structuredData)
        {
            return !string.IsNullOrWhiteSpace(structuredData.FullName)
                || !string.IsNullOrWhiteSpace(structuredData.CompanyName)
                || !string.IsNullOrWhiteSpace(structuredData.Email)
                || !string.IsNullOrWhiteSpace(structuredData.Tel)
                || !string.IsNullOrWhiteSpace(structuredData.FullAddress);
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => value.GetRawText()
            };
        }

        private static List<string> ExtractErrors(JsonElement element)
        {
            var errors = new List<string>();
            if (!TryGetPropertyIgnoreCase(element, "errors", out var errorsElement)
                || errorsElement.ValueKind != JsonValueKind.Array)
            {
                return errors;
            }

            foreach (var error in errorsElement.EnumerateArray())
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    var value = error.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        errors.Add(value);
                    }
                    continue;
                }

                var raw = error.GetRawText();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    errors.Add(raw);
                }
            }

            return errors;
        }

        private static bool TryCollectPagesFromString(JsonElement element, List<OcrPageResult> pages)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var raw = element.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(raw);
                        return TryCollectPages(document.RootElement, pages);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (TryCollectPagesFromString(property.Value, pages))
                    {
                        return true;
                    }
                }
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryCollectPagesFromString(item, pages))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryParsePage(JsonElement element, out OcrPageResult page)
        {
            page = new OcrPageResult();
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(element, "ocr result", out var blocksElement)
                && !TryGetPropertyIgnoreCase(element, "Ocr Result", out blocksElement)
                && !TryGetPropertyIgnoreCase(element, "ocr_result", out blocksElement))
            {
                return false;
            }

            if (TryGetPropertyIgnoreCase(element, "page", out var pageElement) && pageElement.TryGetInt32(out var pageNumber))
            {
                page.Page = pageNumber;
            }

            if (blocksElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var blockElement in blocksElement.EnumerateArray())
            {
                var text = TryGetPropertyIgnoreCase(blockElement, "text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var block = new OcrTextBlock
                {
                    Text = text,
                    Page = page.Page
                };

                if (TryGetPropertyIgnoreCase(blockElement, "location", out var locationElement)
                    && locationElement.ValueKind == JsonValueKind.Array)
                {
                    block.Location = locationElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.Number)
                        .Select(item => item.GetInt32())
                        .ToArray();
                }

                page.Blocks.Add(block);
            }

            return page.Blocks.Count > 0;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string BuildPayloadPreview(JsonElement data)
        {
            try
            {
                var raw = data.GetRawText();
                return raw.Length <= 1200 ? raw : raw[..1200] + "...";
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static bool IsFailedStatus(string jobStatus)
        {
            return string.Equals(jobStatus, "failed", StringComparison.OrdinalIgnoreCase);
        }
    }
}

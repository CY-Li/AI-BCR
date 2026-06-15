using System.IO;
using System.Runtime.InteropServices;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Models.Recognition;
using PlustekBCR.Services.Plustek;

namespace PlustekBCR.Services.Recognition
{
    public class BusinessCardRecognitionService : IBusinessCardRecognitionService
    {
        private readonly IPlustekRecognitionCoordinator _recognitionCoordinator;
        private readonly IRecognitionDiagnosticsService _diagnosticsService;

        public BusinessCardRecognitionService(
            IPlustekRecognitionCoordinator recognitionCoordinator,
            IRecognitionDiagnosticsService diagnosticsService)
        {
            _recognitionCoordinator = recognitionCoordinator;
            _diagnosticsService = diagnosticsService;
        }

        public async Task RecognizeAsync(BusinessCard card, CancellationToken cancellationToken = default)
        {
            var imageBytes = card.FrontImageData;
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new RecognitionException(RecognitionFailureKind.InvalidInput, "Business card does not contain front image data.");
            }

            ValidateImageInput(imageBytes);

            var request = new RecognitionRequest
            {
                BusinessCardId = card.Id,
                ImageBytes = imageBytes,
                FileName = BuildFileName(card),
                ContentType = InferContentType(imageBytes),
                Market = card.MarketCode,
                SourceType = card.IsAutoScanSession ? RecognitionSourceType.Scan : RecognitionSourceType.ImportImage
            };

            await _diagnosticsService.LogAsync("BusinessCardRecognition", $"Card={card.Id} start recognition. Market={request.Market}, Source={request.SourceType}, Bytes={request.ImageBytes.Length}, ContentType={request.ContentType}.", cancellationToken);
            var documentResult = await _recognitionCoordinator.RecognizeAsync(request, cancellationToken);
            await _diagnosticsService.LogAsync("BusinessCardRecognition", $"Card={card.Id} recognition response received. Structured={documentResult.StructuredData != null}, Pages={documentResult.Pages.Count}.", cancellationToken);
            var recognizedData = documentResult.StructuredData ?? new RecognizedBusinessCardData();
            await _diagnosticsService.LogAsync("BusinessCardRecognition",
                $"Card={card.Id} market={documentResult.Market} structured-only mode fields: Name='{recognizedData.FullName}', Company='{recognizedData.CompanyName}', Email='{recognizedData.Email}'.",
                cancellationToken);
            await _diagnosticsService.LogAsync("StructuredParse",
                $"Card={card.Id} market={documentResult.Market} parsed address: Line1='{recognizedData.AddressLine1}', Line2='{recognizedData.AddressLine2}', City='{recognizedData.City}', State='{recognizedData.State}', Zip='{recognizedData.ZipCode}', Country='{recognizedData.Country}', FullAddress='{recognizedData.FullAddress}'.",
                cancellationToken);
            try
            {
                await ApplyRecognizedDataOnUiThreadAsync(card, recognizedData);
                await _diagnosticsService.LogAsync("UiUpdate",
                    $"Card={card.Id} market={card.MarketCode} applied card address: Line1='{card.AddressLine1}', Line2='{card.AddressLine2}', City='{card.City}', State='{card.State}', Zip='{card.ZipCode}', Country='{card.Country}', FullAddress='{card.FullAddress}'.",
                    cancellationToken);
            }
            catch (Exception ex) when (ex is COMException || ex is InvalidOperationException)
            {
                await _diagnosticsService.LogAsync("UiUpdate", $"Card={card.Id} UI update failed: {ex}", cancellationToken);
                throw new RecognitionException(RecognitionFailureKind.UiUpdateFailed, "AI result received but UI update failed.", ex);
            }
        }

        private static void ApplyRecognizedData(BusinessCard card, RecognizedBusinessCardData data)
        {
            ApplySharedStructuredData(card, data);

            if (card.MarketCode == MarketCode.US)
            {
                card.MiddleName = NormalizeStructuredValue(data.MiddleName);
                card.Suffix = NormalizeStructuredValue(data.Suffix);
                card.Extension = NormalizeStructuredValue(data.Extension);
            }
            else
            {
                card.LastNameKana = NormalizeStructuredValue(data.LastNameKana);
                card.FirstNameKana = NormalizeStructuredValue(data.FirstNameKana);
                card.FullNameKana = NormalizeStructuredValue(data.FullNameKana);
            }

            BusinessCardFieldAccessor.SetIdentityFromFullName(card);
            card.PopulateDerivedFieldsFromStructuredValues();
        }

        private static void ApplySharedStructuredData(BusinessCard card, RecognizedBusinessCardData data)
        {
            card.LastName = NormalizeStructuredValue(data.LastName);
            card.FirstName = NormalizeStructuredValue(data.FirstName);
            card.FullName = NormalizeStructuredValue(data.FullName);
            card.CompanyName = NormalizeStructuredValue(data.CompanyName);
            card.Department1 = NormalizeStructuredValue(data.Department1);
            card.Department2 = NormalizeStructuredValue(data.Department2);
            card.Department3 = NormalizeStructuredValue(data.Department3);
            card.Department4 = NormalizeStructuredValue(data.Department4);
            card.DepartmentFull = NormalizeStructuredValue(data.DepartmentFull);
            card.JobTitle = NormalizeStructuredValue(data.JobTitle);
            card.Email = NormalizeStructuredValue(data.Email);
            card.Tel = NormalizeStructuredValue(data.Tel);
            card.Mobile = NormalizeStructuredValue(data.Mobile);
            card.Fax = NormalizeStructuredValue(data.Fax);
            card.Website = NormalizeStructuredValue(data.Website);
            card.AddressLine1 = NormalizeStructuredValue(data.AddressLine1);
            card.AddressLine2 = NormalizeStructuredValue(data.AddressLine2);
            card.City = NormalizeStructuredValue(data.City);
            card.State = NormalizeStructuredValue(data.State);
            card.ZipCode = NormalizeStructuredValue(data.ZipCode);
            card.Country = NormalizeStructuredValue(data.Country);
            card.FullAddress = NormalizeStructuredValue(data.FullAddress);
        }

        private static string NormalizeStructuredValue(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static Task ApplyRecognizedDataOnUiThreadAsync(BusinessCard card, RecognizedBusinessCardData data)
        {
            var tcs = new TaskCompletionSource<object?>();
            var dispatcher = App.Window?.DispatcherQueue;
            if (dispatcher == null)
            {
                try
                {
                    card.SuppressAutoZipLookup = true;
                    ApplyRecognizedData(card, data);
                    tcs.SetResult(null);
                }
                finally
                {
                    card.SuppressAutoZipLookup = false;
                }
                return tcs.Task;
            }

            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    card.SuppressAutoZipLookup = true;
                    ApplyRecognizedData(card, data);
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    card.SuppressAutoZipLookup = false;
                }
            });

            return tcs.Task;
        }

        private static string BuildFileName(BusinessCard card)
        {
            var baseName = string.IsNullOrWhiteSpace(card.FullName) ? card.Id.ToString("N") : card.FullName;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalid, '_');
            }

            return $"{baseName}.jpg";
        }

        private static string InferContentType(byte[] imageBytes)
        {
            if (imageBytes.Length >= 8
                && imageBytes[0] == 0x89
                && imageBytes[1] == 0x50
                && imageBytes[2] == 0x4E
                && imageBytes[3] == 0x47)
            {
                return "image/png";
            }

            return "image/jpeg";
        }

        private static void ValidateImageInput(byte[] imageBytes)
        {
            const int maxImageBytes = 10 * 1024 * 1024;
            if (imageBytes.Length == 0)
            {
                throw new RecognitionException(RecognitionFailureKind.InvalidInput, "Business card image is empty.");
            }

            if (imageBytes.Length > maxImageBytes)
            {
                throw new RecognitionException(RecognitionFailureKind.InvalidInput, "Business card image exceeds the supported size limit.");
            }

            if (!IsSupportedImage(imageBytes))
            {
                throw new RecognitionException(RecognitionFailureKind.InvalidInput, "Business card image format is not supported for AI recognition.");
            }
        }

        private static bool IsSupportedImage(byte[] imageBytes)
        {
            var isPng = imageBytes.Length >= 8
                && imageBytes[0] == 0x89
                && imageBytes[1] == 0x50
                && imageBytes[2] == 0x4E
                && imageBytes[3] == 0x47;

            var isJpeg = imageBytes.Length >= 3
                && imageBytes[0] == 0xFF
                && imageBytes[1] == 0xD8
                && imageBytes[2] == 0xFF;

            return isPng || isJpeg;
        }
    }
}

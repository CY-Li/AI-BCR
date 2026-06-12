using PlustekBCR.Models.Ocr;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Plustek
{
    public interface IPlustekOcrCoordinator
    {
        Task<OcrDocumentResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken = default);
    }
}

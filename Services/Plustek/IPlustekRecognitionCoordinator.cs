using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Plustek
{
    public interface IPlustekRecognitionCoordinator
    {
        Task<RecognitionDocumentResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken = default);
    }
}

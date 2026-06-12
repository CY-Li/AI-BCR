using PlustekBCR.Models;

namespace PlustekBCR.Services.Recognition
{
    public interface IBusinessCardRecognitionService
    {
        Task RecognizeAsync(BusinessCard card, CancellationToken cancellationToken = default);
    }
}

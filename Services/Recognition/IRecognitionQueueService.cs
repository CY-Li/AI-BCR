using PlustekBCR.Models;

namespace PlustekBCR.Services.Recognition
{
    public interface IRecognitionQueueService
    {
        Task EnqueueAsync(BusinessCard card, CancellationToken cancellationToken = default);
        Task EnqueueAsync(IEnumerable<BusinessCard> cards, CancellationToken cancellationToken = default);
    }
}

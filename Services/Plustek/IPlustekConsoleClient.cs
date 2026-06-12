using PlustekBCR.Models;
using PlustekBCR.Models.Plustek;

namespace PlustekBCR.Services.Plustek
{
    public interface IPlustekConsoleClient
    {
        Task<string> IssueProjectTokenAsync(MarketCode market, CancellationToken cancellationToken = default);
        Task<UploadUrlItem> CreateUploadUrlAsync(MarketCode market, string fileName, string contentType, CancellationToken cancellationToken = default);
        Task UploadFileAsync(string signedUrl, byte[] imageBytes, string contentType, CancellationToken cancellationToken = default);
        Task ConfirmUploadAsync(MarketCode market, string fileId, string fileName, string contentType, CancellationToken cancellationToken = default);
        Task<string> CreateDocumentAsync(MarketCode market, string fileId, CancellationToken cancellationToken = default);
        Task<string> CreateEscanJobAsync(MarketCode market, string documentId, CancellationToken cancellationToken = default);
        Task<EscanJobResultResponse> GetEscanJobResultAsync(MarketCode market, string jobId, CancellationToken cancellationToken = default);
    }
}

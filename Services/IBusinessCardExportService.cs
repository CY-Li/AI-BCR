using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IBusinessCardExportService
    {
        Task<int> ExportCsvAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            CancellationToken cancellationToken = default);
    }
}

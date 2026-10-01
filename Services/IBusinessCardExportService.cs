using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IBusinessCardExportService
    {
        Task<int> ExportAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            ContactExportFormat format,
            CancellationToken cancellationToken = default)
        {
            if (format != ContactExportFormat.Csv)
            {
                throw new NotSupportedException($"The export format '{format}' is not supported by this implementation.");
            }

            return ExportCsvAsync(cards, destinationPath, cancellationToken);
        }

        Task<int> ExportCsvAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            CancellationToken cancellationToken = default);
    }
}

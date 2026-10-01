using System;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IExportFilePickerService
    {
        Task<string?> PickDestinationAsync(string suggestedFileName, ContactExportFormat format)
        {
            if (format != ContactExportFormat.Csv)
            {
                throw new NotSupportedException($"The export format '{format}' is not supported by this implementation.");
            }

            return PickCsvDestinationAsync(suggestedFileName);
        }

        Task<string?> PickCsvDestinationAsync(string suggestedFileName);
    }
}

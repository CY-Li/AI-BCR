using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public interface IExportFilePickerService
    {
        Task<string?> PickCsvDestinationAsync(string suggestedFileName);
    }
}

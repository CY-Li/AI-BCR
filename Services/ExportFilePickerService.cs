using System.Collections.Generic;
using System.Threading.Tasks;
using PlustekBCR.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PlustekBCR.Services
{
    public sealed class ExportFilePickerService : IExportFilePickerService
    {
        public Task<string?> PickCsvDestinationAsync(string suggestedFileName) =>
            PickDestinationAsync(suggestedFileName, Models.ContactExportFormat.Csv);

        public async Task<string?> PickDestinationAsync(
            string suggestedFileName,
            Models.ContactExportFormat format)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };

            if (format == Models.ContactExportFormat.VCard)
            {
                picker.FileTypeChoices.Add("vCard contact file", new List<string> { ".vcf" });
            }
            else if (format == Models.ContactExportFormat.PlainText)
            {
                picker.FileTypeChoices.Add("Plain text file", new List<string> { ".txt" });
            }
            else
            {
                picker.FileTypeChoices.Add("CSV (Comma delimited)", new List<string> { ".csv" });
            }

            PickerWindowHelper.Initialize(picker);

            StorageFile? file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using PlustekBCR.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PlustekBCR.Services
{
    public sealed class ExportFilePickerService : IExportFilePickerService
    {
        public async Task<string?> PickCsvDestinationAsync(string suggestedFileName)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };
            picker.FileTypeChoices.Add("CSV (Comma delimited)", new List<string> { ".csv" });
            PickerWindowHelper.Initialize(picker);

            StorageFile? file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
    }
}

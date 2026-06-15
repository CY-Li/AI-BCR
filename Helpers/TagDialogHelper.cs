using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PlustekBCR.Services;

namespace PlustekBCR.Helpers
{
    public static class TagDialogHelper
    {
        public static async Task<string?> PromptForNewTagAsync(XamlRoot? xamlRoot)
        {
            var localization = App.GetService<ILocalizationService>();
            var input = new TextBox
            {
                PlaceholderText = localization.GetString("Tag.Add.Placeholder")
            };

            var dialog = DialogHelper.CreateDialog(
                xamlRoot,
                localization.GetString("Tag.Add.Title"),
                input,
                primaryButtonText: localization.GetString("Button.Add"),
                closeButtonText: localization.GetString("Button.Cancel"));

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            var value = TagTextHelper.Normalize(input.Text);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

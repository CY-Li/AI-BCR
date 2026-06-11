using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PlustekBCR.Helpers
{
    public static class TagDialogHelper
    {
        public static async Task<string?> PromptForNewTagAsync(XamlRoot? xamlRoot)
        {
            var input = new TextBox
            {
                PlaceholderText = "Enter a new tag"
            };

            var dialog = DialogHelper.CreateDialog(
                xamlRoot,
                "Add tag",
                input,
                primaryButtonText: "Add",
                closeButtonText: "Cancel");

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

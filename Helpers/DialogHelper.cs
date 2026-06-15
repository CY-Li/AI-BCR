using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PlustekBCR.Services;

namespace PlustekBCR.Helpers
{
    public static class DialogHelper
    {
        public static ContentDialog CreateDialog(
            XamlRoot? xamlRoot,
            string title,
            object content,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? closeButtonText = null,
            ContentDialogButton defaultButton = ContentDialogButton.Primary)
        {
            return new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = defaultButton,
                XamlRoot = xamlRoot
            };
        }

        public static Task<ContentDialogResult> ShowMessageAsync(XamlRoot? xamlRoot, string title, string content, string? closeButtonText = null)
        {
            var dialog = CreateDialog(
                xamlRoot,
                title,
                content,
                closeButtonText: closeButtonText ?? App.GetService<ILocalizationService>().GetString("Button.OK"),
                defaultButton: ContentDialogButton.Close);

            return dialog.ShowAsync().AsTask();
        }
    }
}

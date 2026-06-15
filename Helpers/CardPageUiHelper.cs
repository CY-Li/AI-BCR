using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PlustekBCR.Models;
using PlustekBCR.Services;

namespace PlustekBCR.Helpers
{
    public static class CardPageUiHelper
    {
        public static ContentDialog CreateDeleteConfirmationDialog(string? fullName, XamlRoot? xamlRoot)
        {
            var localization = App.GetService<ILocalizationService>();
            return DialogHelper.CreateDialog(
                xamlRoot,
                localization.GetString("Dialog.DeleteCard.Title"),
                localization.Format("Dialog.DeleteCard.Message", fullName ?? string.Empty),
                primaryButtonText: localization.GetString("Button.Delete"),
                closeButtonText: localization.GetString("Button.Cancel"),
                defaultButton: ContentDialogButton.Close);
        }

        public static void RebuildTagFlowItems(ObservableCollection<TagFlowItem> target, IEnumerable<string> tags)
        {
            target.Clear();
            foreach (var tag in tags)
            {
                target.Add(new TagFlowItem { Text = tag, IsAddButton = false });
            }
        }
    }
}

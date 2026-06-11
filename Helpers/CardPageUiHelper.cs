using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PlustekBCR.Models;

namespace PlustekBCR.Helpers
{
    public static class CardPageUiHelper
    {
        public static ContentDialog CreateDeleteConfirmationDialog(string? fullName, XamlRoot? xamlRoot)
        {
            return DialogHelper.CreateDialog(
                xamlRoot,
                "Delete Business Card",
                $"Are you sure you want to delete the business card of '{fullName}'? This action cannot be undone.",
                primaryButtonText: "Delete",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Close);
        }

        public static async Task RunMockAiReprocessAsync(BusinessCard card)
        {
            if (card.Status == ProcessingStatus.Recognizing)
            {
                return;
            }

            var previousStatus = card.Status;
            card.Status = ProcessingStatus.Recognizing;

            try
            {
                await Task.Delay(1800);
                card.Status = ProcessingStatus.Done;
            }
            catch
            {
                card.Status = previousStatus;
                throw;
            }
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

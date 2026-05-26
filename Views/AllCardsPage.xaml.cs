#nullable enable
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using Microsoft.UI.Xaml.Media.Animation;
using PlustekBCR.ViewModels;
using PlustekBCR.Models;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace PlustekBCR.Views
{
    public sealed partial class AllCardsPage : Page
    {
        public AllCardsViewModel ViewModel { get; }

        public AllCardsPage()
        {
            ViewModel = App.GetService<AllCardsViewModel>();
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            ViewModel.ConfirmDeleteCardAsync = async (card) =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Business Card",
                    Content = $"Are you sure you want to delete the business card of '{card.Name}'? This action cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            };
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Handle backward animation when returning from CardDetailPage
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
            if (animation != null)
            {
                animation.TryStart(SidebarFrontImage);
            }
        }

        private void OnCardClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PlustekBCR.Models.BusinessCard card)
            {
                ViewModel.SelectCardCommand.Execute(card);
            }
        }

        private void OnOverlayClicked(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Only close if the sidebar is open and we clicked outside the sidebar AND outside any card
            if (ViewModel.IsSidebarOpen)
            {
                // In WinUI 3, we must use VisualTreeHelper to reliably traverse the visual tree, 
                // especially for elements inside ControlTemplates where .Parent might be null.
                Microsoft.UI.Xaml.DependencyObject? visualParent = e.OriginalSource as Microsoft.UI.Xaml.DependencyObject;
                while (visualParent != null)
                {
                    if (visualParent == Sidebar) return; // Clicked inside sidebar, don't close
                    if (visualParent is GridViewItem || visualParent is ListViewItem || visualParent.GetType().Name == "GridViewItem" || visualParent.GetType().Name == "ListViewItem") return; // Clicked a card, don't close
                    visualParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(visualParent);
                }

                ViewModel.CloseSidebarCommand.Execute(null);
            }
        }

        private void Card_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                // Visual state changes
                // 1. Keep border simple (no more blue line)
                // We'll just use the shadow and scale for the "glow" effect as requested
                
                // 2. Lift the ZIndex so the card stays on top of neighbors
                if (grid.Parent is ContentPresenter cp && cp.Parent is Grid container)
                {
                    if (container.Parent is GridViewItem gridItem) Canvas.SetZIndex(gridItem, 100);
                    else if (container.Parent is ListViewItem listItem) Canvas.SetZIndex(listItem, 100);
                }

                // 3. Safe animations using UIElement properties
                grid.CenterPoint = new Vector3((float)grid.ActualWidth / 2, (float)grid.ActualHeight / 2, 0);
                grid.Scale = new Vector3(1.02f, 1.02f, 1.0f);
                grid.Translation = new Vector3(0, -4, 64); // Deeper shadow (Z) and subtle lift (Y)
            }
        }

        private void Card_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                // Reset ZIndex
                if (grid.Parent is ContentPresenter cp && cp.Parent is Grid container)
                {
                    if (container.Parent is GridViewItem gridItem) Canvas.SetZIndex(gridItem, 0);
                    else if (container.Parent is ListViewItem listItem) Canvas.SetZIndex(listItem, 0);
                }

                // Reset safe animations to default state (keeps base shadow)
                grid.Scale = new Vector3(1.0f, 1.0f, 1.0f);
                grid.Translation = new Vector3(0, 0, 16); 
            }
        }

        private void OnEditInfoClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard != null)
            {
                var navParams = new NavigationParams
                {
                    AllCards = ViewModel.AllCards,
                    SelectedCard = ViewModel.SelectedCard
                };

                // Prepare connected animation
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", SidebarFrontImage);

                this.Frame.Navigate(typeof(CardDetailPage), navParams);
            }
        }

        private void OnSidebarSplitterDragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            var newWidth = Sidebar.Width - e.HorizontalChange;
            if (newWidth < 280) newWidth = 280;
            if (newWidth > 1200) newWidth = 1200;
            Sidebar.Width = newWidth;
            UpdateCardImagesLayout(newWidth);
        }

        // Threshold (px) above which both front and back images are shown side-by-side
        private const double WideModeSidebarThreshold = 520;

        private void OnSidebarSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            UpdateCardImagesLayout(e.NewSize.Width);
        }

        private void UpdateCardImagesLayout(double sidebarWidth)
        {
            if (BackImageColumn == null) return;
            if (sidebarWidth >= WideModeSidebarThreshold)
            {
                // Wide mode: give back image an equal share
                BackImageColumn.Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star);
            }
            else
            {
                // Narrow mode: collapse the back image column
                BackImageColumn.Width = new Microsoft.UI.Xaml.GridLength(0);
            }
        }

        private void SidebarSplitter_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
        }

        private void SidebarSplitter_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        }

        private async void OnDeleteContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is PlustekBCR.Models.BusinessCard card)
            {
                if (ViewModel.DeleteCardCommand.CanExecute(card))
                {
                    await ViewModel.DeleteCardCommand.ExecuteAsync(card);
                }
            }
        }

        private void OnViewDetailsContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is PlustekBCR.Models.BusinessCard card)
            {
                ViewModel.SelectedCard = card;
                OnEditInfoClicked(sender, e);
            }
        }

        private async void OnExportCsvContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is BusinessCard card)
            {
                await ExportCardAsCsvAsync(card);
            }
        }

        private async void OnExportTxtContextClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is BusinessCard card)
            {
                await ExportCardAsTxtAsync(card);
            }
        }

        private async void OnAiReprocessClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            var card = ViewModel.SelectedCard;
            if (card.Status == ProcessingStatus.Recognizing)
            {
                return;
            }

            var prevStatus = card.Status;
            card.Status = ProcessingStatus.Recognizing;

            try
            {
                await Task.Delay(1800);
                card.Status = ProcessingStatus.Done;
            }
            catch
            {
                card.Status = prevStatus;
                throw;
            }
        }

        private async Task ExportCardAsCsvAsync(BusinessCard card)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = string.IsNullOrWhiteSpace(card.Name) ? "business_card" : card.Name
            };
            picker.FileTypeChoices.Add("CSV (Comma delimited)", new System.Collections.Generic.List<string> { ".csv" });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var headers = new[]
            {
                "Name","Title","Company","Phone","Email","Address","Country","Website","Tag","ScanDate","Status"
            };
            var values = new[]
            {
                card.Name, card.Title, card.Company, card.Phone, card.Email, card.Address, card.Country, card.Website, card.Tag,
                card.ScanDate.ToString("yyyy-MM-dd HH:mm:ss"), card.Status.ToString()
            };

            var csv = string.Join(",", headers) + Environment.NewLine +
                      string.Join(",", System.Array.ConvertAll(values, EscapeCsv));
            await File.WriteAllTextAsync(file.Path, csv, Encoding.UTF8);
        }

        private async Task ExportCardAsTxtAsync(BusinessCard card)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = string.IsNullOrWhiteSpace(card.Name) ? "business_card" : card.Name
            };
            picker.FileTypeChoices.Add("Text File", new System.Collections.Generic.List<string> { ".txt" });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {card.Name}");
            sb.AppendLine($"Title: {card.Title}");
            sb.AppendLine($"Company: {card.Company}");
            sb.AppendLine($"Phone: {card.Phone}");
            sb.AppendLine($"Email: {card.Email}");
            sb.AppendLine($"Address: {card.Address}");
            sb.AppendLine($"Country: {card.Country}");
            sb.AppendLine($"Website: {card.Website}");
            sb.AppendLine($"Tag: {card.Tag}");
            sb.AppendLine($"Scan Date: {card.ScanDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Status: {card.Status}");

            await File.WriteAllTextAsync(file.Path, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('\"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PlustekBCR.ViewModels;
using PlustekBCR.Models;

namespace PlustekBCR.Views
{
    public sealed partial class CardDetailPage : Page
    {
        public CardDetailViewModel ViewModel { get; }

        public CardDetailPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<CardDetailViewModel>();

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

            ViewModel.NavigateBackRequested = () =>
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Hide main sidebar using MainViewModel binding
            if (ViewModel?.MainViewModel != null)
            {
                ViewModel.MainViewModel.IsPaneVisible = false;
            }

            if (e.Parameter is NavigationParams navParams)
            {
                ViewModel?.Initialize(navParams.AllCards, navParams.SelectedCard);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Restore main sidebar
            if (ViewModel?.MainViewModel != null)
            {
                ViewModel.MainViewModel.IsPaneVisible = true;
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                // Prepare backward connected animation
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackwardConnectedAnimation", FrontDetailImage);

                // Navigate back
                Frame.GoBack(new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Try to start the forward connected animation
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            if (animation != null)
            {
                animation.TryStart(FrontDetailImage);
            }
        }

        private async void OnUploadFrontFileClicked(object sender, RoutedEventArgs e)
        {
            var file = await PickImageFileAsync();
            if (file != null)
            {
                await LoadImageToFrontAsync(file);
            }
        }

        private async void OnScanFrontClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.MainViewModel != null && !ViewModel.MainViewModel.SkipScanDialog)
            {
                if (ViewModel.MainViewModel.ShowScanConfirmationDialogAsync != null)
                {
                    var result = await ViewModel.MainViewModel.ShowScanConfirmationDialogAsync();
                    if (!result.proceed) return;
                    if (result.skip) ViewModel.MainViewModel.SkipScanDialog = true;
                }
            }

            await SimulateScanAsync(isFront: true);
        }

        private void OnDeleteFrontClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard != null)
            {
                ViewModel.SelectedCard.FrontImageData = null;
            }
        }

        private async void OnUploadBackFileClicked(object sender, RoutedEventArgs e)
        {
            var file = await PickImageFileAsync();
            if (file != null)
            {
                await LoadImageToBackAsync(file);
            }
        }

        private async void OnScanBackClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.MainViewModel != null && !ViewModel.MainViewModel.SkipScanDialog)
            {
                if (ViewModel.MainViewModel.ShowScanConfirmationDialogAsync != null)
                {
                    var result = await ViewModel.MainViewModel.ShowScanConfirmationDialogAsync();
                    if (!result.proceed) return;
                    if (result.skip) ViewModel.MainViewModel.SkipScanDialog = true;
                }
            }

            await SimulateScanAsync(isFront: false);
        }

        private async Task SimulateScanAsync(bool isFront)
        {
            if (ViewModel.SelectedCard != null)
            {
                var prevStatus = ViewModel.SelectedCard.Status;
                ViewModel.SelectedCard.Status = ProcessingStatus.Recognizing;

                if (isFront)
                {
                    FrontScanLoadingOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    BackScanLoadingOverlay.Visibility = Visibility.Visible;
                }

                try
                {
                    await Task.Delay(2200);

                    // Demo: load a bundled sample card image as scan result.
                    var sampleFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/BusinessCard_01.jpg"));
                    var scannedBytes = await FileToByteArrayAsync(sampleFile);

                    if (isFront)
                    {
                        ViewModel.SelectedCard.FrontImageData = scannedBytes;
                    }
                    else
                    {
                        ViewModel.SelectedCard.BackImageData = scannedBytes;
                    }
                }
                finally
                {
                    ViewModel.SelectedCard.Status = prevStatus;

                    if (isFront)
                    {
                        FrontScanLoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        BackScanLoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void OnDeleteBackClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard != null)
            {
                ViewModel.SelectedCard.BackImageData = null;
            }
        }

        private async void OnAiReprocessClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            var card = ViewModel.SelectedCard;
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

        private void OnImageDragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Drop to upload image";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
                e.Handled = true;

                if (sender is FrameworkElement element)
                {
                    element.Opacity = 0.6; // Visual feedback for drag over
                }
            }
            else
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            }
        }

        private void OnImageDragLeave(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0; // Reset visual feedback
            }
        }

        private async void OnFrontImageDrop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }

            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    if (IsImageFile(file))
                    {
                        await LoadImageToFrontAsync(file);
                    }
                }
            }
        }

        private async void OnBackImageDrop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }

            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    if (IsImageFile(file))
                    {
                        await LoadImageToBackAsync(file);
                    }
                }
            }
        }

        private async Task<StorageFile?> PickImageFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            return await picker.PickSingleFileAsync();
        }

        private async Task LoadImageToFrontAsync(StorageFile file)
        {
            if (ViewModel.SelectedCard != null)
            {
                ViewModel.SelectedCard.FrontImageData = await FileToByteArrayAsync(file);
            }
        }

        private async Task LoadImageToBackAsync(StorageFile file)
        {
            if (ViewModel.SelectedCard != null)
            {
                ViewModel.SelectedCard.BackImageData = await FileToByteArrayAsync(file);
            }
        }

        private async Task<byte[]> FileToByteArrayAsync(StorageFile file)
        {
            using (var stream = await file.OpenReadAsync())
            {
                var buffer = new byte[stream.Size];
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(buffer);
                }
                return buffer;
            }
        }

        private bool IsImageFile(StorageFile file)
        {
            string ext = file.FileType.ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";
        }
    }

    public class NavigationParams
    {
        public System.Collections.ObjectModel.ObservableCollection<BusinessCard> AllCards { get; set; } = new System.Collections.ObjectModel.ObservableCollection<BusinessCard>();
        public BusinessCard? SelectedCard { get; set; }
    }
}

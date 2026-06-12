using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PlustekBCR.Helpers;
using PlustekBCR.ViewModels;
using PlustekBCR.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using PlustekBCR.Services;

namespace PlustekBCR.Views
{
    public sealed partial class CardDetailPage : Page
    {
        private const int PageEnterDurationMs = 140;
        private const int PageExitDurationMs = 140;
        private const double PageEnterOffsetX = 36d;
        private const double PageExitOffsetX = 36d;
        public CardDetailViewModel ViewModel { get; }
        public ObservableCollection<TagFlowItem> EditTagFlowItems { get; } = new();
        private readonly IApplicationSettingsService _settingsService;
        private bool _isTransitionRunning;

        public CardDetailPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<CardDetailViewModel>();
            _settingsService = App.GetService<IApplicationSettingsService>();

            ViewModel.ConfirmDeleteCardAsync = async (card) =>
            {
                var dialog = CardPageUiHelper.CreateDeleteConfirmationDialog(card.FullName, this.XamlRoot);
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

            ViewModel.SelectedTags.CollectionChanged += OnSelectedTagsCollectionChanged;
            _settingsService.CurrentMarketChanged += OnCurrentMarketChanged;
            Unloaded += OnPageUnloaded;
            RebuildEditTagFlowItems();
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

        private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildEditTagFlowItems();
        }

        private void OnCurrentMarketChanged(MarketCode market)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Bindings.Update();
            });
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _settingsService.CurrentMarketChanged -= OnCurrentMarketChanged;
            Unloaded -= OnPageUnloaded;
        }

        private void RebuildEditTagFlowItems()
        {
            CardPageUiHelper.RebuildTagFlowItems(EditTagFlowItems, ViewModel.SelectedTags);
        }

        private async void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (!Frame.CanGoBack || _isTransitionRunning)
            {
                return;
            }

            await PlayPageTransitionAsync(isEntering: false);
            Frame.GoBack(new SuppressNavigationTransitionInfo());
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await PlayPageTransitionAsync(isEntering: true);
        }

        private async void OnUploadFrontFileClicked(object sender, RoutedEventArgs e)
        {
            await HandleImageUploadAsync(isFront: true);
        }

        private async void OnScanFrontClicked(object sender, RoutedEventArgs e)
        {
            if (!await EnsureScanConfirmedAsync())
            {
                return;
            }

            await SimulateScanAsync(isFront: true);
        }

        private void OnDeleteFrontClicked(object sender, RoutedEventArgs e)
        {
            ClearSelectedImage(isFront: true);
        }

        private async void OnUploadBackFileClicked(object sender, RoutedEventArgs e)
        {
            await HandleImageUploadAsync(isFront: false);
        }

        private async void OnScanBackClicked(object sender, RoutedEventArgs e)
        {
            if (!await EnsureScanConfirmedAsync())
            {
                return;
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

                    SetSelectedImageData(scannedBytes, isFront);
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
            ClearSelectedImage(isFront: false);
        }

        private void OnNewNoteTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            if (sender is TextBox textBox)
            {
                ViewModel.NewNoteContent = textBox.Text;
            }

            if (ViewModel.AddNoteCommand.CanExecute(null))
            {
                ViewModel.AddNoteCommand.Execute(null);
            }

            UpdateNewNotePlaceholderVisibility(ViewModel.NewNoteContent);
            e.Handled = true;
        }

        private void OnNewNoteTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                UpdateNewNotePlaceholderVisibility(textBox.Text);
            }
        }

        private void UpdateNewNotePlaceholderVisibility(string? text)
        {
            if (NewNotePlaceholderText == null)
            {
                return;
            }

            NewNotePlaceholderText.Visibility = string.IsNullOrWhiteSpace(text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void OnAiReprocessClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            await ViewModel.ReprocessAiAsync(ViewModel.SelectedCard);
        }

        private async void OnAddTagClicked(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout
            {
                MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["BcrTagMenuFlyoutPresenterStyle"]
            };
            var available = ViewModel.AvailableTags
                .Where(tag => !TagTextHelper.ContainsIgnoreCase(ViewModel.SelectedTags, tag))
                .ToList();

            foreach (var tag in available)
            {
                var item = new MenuFlyoutItem { Text = tag, Tag = tag };
                item.Click += async (_, __) => { await ViewModel.AddSelectedTagAsync(tag); };
                flyout.Items.Add(item);
            }

            if (available.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var newTagItem = new MenuFlyoutItem { Text = "+ New tag" };
            newTagItem.Click += async (_, __) =>
            {
                var value = await TagDialogHelper.PromptForNewTagAsync(this.XamlRoot);
                await ViewModel.AddSelectedTagAsync(value);
            };
            flyout.Items.Add(newTagItem);
            flyout.ShowAt((FrameworkElement)sender);
        }

        private void OnRemoveTagClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                ViewModel.RemoveSelectedTag(tag);
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
            await HandleImageDropAsync(sender, e, isFront: true);
        }

        private async void OnBackImageDrop(object sender, DragEventArgs e)
        {
            await HandleImageDropAsync(sender, e, isFront: false);
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
            PickerWindowHelper.Initialize(picker);

            return await picker.PickSingleFileAsync();
        }

        private async Task HandleImageUploadAsync(bool isFront)
        {
            var file = await PickImageFileAsync();
            if (file == null)
            {
                return;
            }

            await LoadImageAsync(file, isFront);
        }

        private async Task HandleImageDropAsync(object sender, DragEventArgs e, bool isFront)
        {
            ResetDropZoneOpacity(sender);

            if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                return;
            }

            e.Handled = true;
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0 || items[0] is not StorageFile file || !IsImageFile(file))
            {
                return;
            }

            await LoadImageAsync(file, isFront);
        }

        private async Task LoadImageAsync(StorageFile file, bool isFront)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            var imageBytes = await FileToByteArrayAsync(file);
            SetSelectedImageData(imageBytes, isFront);
        }

        private void ClearSelectedImage(bool isFront)
        {
            SetSelectedImageData(null, isFront);
        }

        private void SetSelectedImageData(byte[]? imageData, bool isFront)
        {
            if (ViewModel.SelectedCard == null)
            {
                return;
            }

            if (isFront)
            {
                ViewModel.SelectedCard.FrontImageData = imageData;
                return;
            }

            ViewModel.SelectedCard.BackImageData = imageData;
        }

        private static void ResetDropZoneOpacity(object sender)
        {
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }
        }

        private async Task<bool> EnsureScanConfirmedAsync()
        {
            if (ViewModel.MainViewModel == null || ViewModel.MainViewModel.SkipScanDialog)
            {
                return true;
            }

            if (ViewModel.MainViewModel.ShowScanConfirmationDialogAsync == null)
            {
                return true;
            }

            var result = await ViewModel.MainViewModel.ShowScanConfirmationDialogAsync();
            if (result.skip)
            {
                ViewModel.MainViewModel.SkipScanDialog = true;
            }

            return result.proceed;
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

        private async Task PlayPageTransitionAsync(bool isEntering)
        {
            if (_isTransitionRunning)
            {
                return;
            }

            _isTransitionRunning = true;
            SetInteractionEnabled(false);

            var fromOpacity = isEntering ? 1d : 1d;
            var toOpacity = isEntering ? 1d : 0d;
            var fromX = isEntering ? PageEnterOffsetX : 0d;
            var toX = isEntering ? 0d : PageExitOffsetX;
            var duration = isEntering ? PageEnterDurationMs : PageExitDurationMs;

            PageRoot.Opacity = fromOpacity;
            PageRootTranslate.X = fromX;
            PageRootTranslate.Y = 0;
            var storyboard = new Storyboard();
            if (!isEntering)
            {
                AddOpacityAnimation(storyboard, PageRoot, fromOpacity, toOpacity, duration, EasingMode.EaseOut);
            }

            AddTranslateXAnimation(storyboard, PageRootTranslate, fromX, toX, duration, EasingMode.EaseOut);
            await RunTransitionStoryboardAsync(storyboard);

            if (isEntering)
            {
                PageRoot.Opacity = 1;
                PageRootTranslate.X = 0;
                PageRootTranslate.Y = 0;
            }

            _isTransitionRunning = false;
            SetInteractionEnabled(true);
        }

        private void SetInteractionEnabled(bool isEnabled)
        {
            PageRoot.IsHitTestVisible = isEnabled;
        }

        private static void AddOpacityAnimation(Storyboard storyboard, DependencyObject target, double from, double to, int durationMs, EasingMode easingMode)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = easingMode }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
        }

        private static void AddTranslateYAnimation(Storyboard storyboard, DependencyObject target, double from, double to, int durationMs)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Y");
            storyboard.Children.Add(animation);
        }

        private static void AddTranslateXAnimation(Storyboard storyboard, DependencyObject target, double from, double to, int durationMs, EasingMode easingMode)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = easingMode }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "X");
            storyboard.Children.Add(animation);
        }

        private static Task RunTransitionStoryboardAsync(Storyboard storyboard)
        {
            var tcs = new TaskCompletionSource<object?>();

            void OnCompleted(object? sender, object e)
            {
                storyboard.Completed -= OnCompleted;
                tcs.TrySetResult(null);
            }

            storyboard.Completed += OnCompleted;
            storyboard.Begin();
            return tcs.Task;
        }
    }

    public class NavigationParams
    {
        public System.Collections.ObjectModel.ObservableCollection<BusinessCard> AllCards { get; set; } = new System.Collections.ObjectModel.ObservableCollection<BusinessCard>();
        public BusinessCard? SelectedCard { get; set; }
    }
}

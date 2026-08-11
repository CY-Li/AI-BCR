#nullable enable
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PlustekBCR.Helpers;
using PlustekBCR.Models;
using PlustekBCR.Services;
using PlustekBCR.ViewModels;
using Windows.Foundation;
using Windows.System;

namespace PlustekBCR.Views
{
    public sealed partial class ImageViewerWindow : Window
    {
        private readonly ILocalizationService _localizationService;
        private readonly ImageViewerState _state = new();
        private BusinessCard? _card;
        private BitmapImage? _bitmap;
        private bool _isFitMode = true;
        private bool _isPanning;
        private Point _lastPointerPosition;
        private double _translateX;
        private double _translateY;

        public event EventHandler? ViewerClosed;
        public event EventHandler<bool>? AlwaysOnTopChanged;

        public ImageViewerWindow(bool isAlwaysOnTop)
        {
            _localizationService = App.GetService<ILocalizationService>();
            _state.IsAlwaysOnTop = isAlwaysOnTop;

            InitializeComponent();
            RootGrid.DataContext = App.GetService<LocalizedStrings>();

            var manager = WinUIEx.WindowManager.Get(this);
            manager.MinWidth = 640;
            manager.MinHeight = 360;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 640));
            ApplyAlwaysOnTop();
            UpdateLocalizedText();

            _localizationService.LanguageChanged += OnLanguageChanged;
            Closed += OnWindowClosed;
        }

        public void ShowCard(BusinessCard card, CardImageSide side)
        {
            if (!ReferenceEquals(_card, card))
            {
                if (_card != null)
                {
                    _card.PropertyChanged -= OnCardPropertyChanged;
                }

                _card = card;
                _card.PropertyChanged += OnCardPropertyChanged;
            }

            if (!_state.SelectAvailableSide(side, HasImage(card.FrontImageData), HasImage(card.BackImageData)))
            {
                Close();
                return;
            }

            CardNameText.Text = card.DisplayName;
            UpdateSideButtons();
            LoadCurrentImage();
        }

        private static bool HasImage(byte[]? imageData) => imageData is { Length: > 0 };

        private void LoadCurrentImage()
        {
            if (_card == null)
            {
                return;
            }

            var imageData = _state.CurrentSide == CardImageSide.Front
                ? _card.FrontImageData
                : _card.BackImageData;

            if (!HasImage(imageData))
            {
                UpdateImageAvailability();
                return;
            }

            using var memoryStream = new MemoryStream(imageData!, writable: false);
            using var randomAccessStream = memoryStream.AsRandomAccessStream();
            var bitmap = new BitmapImage();
            bitmap.SetSource(randomAccessStream);
            _bitmap = bitmap;
            ViewerImage.Source = bitmap;
            ViewerImage.Width = bitmap.PixelWidth;
            ViewerImage.Height = bitmap.PixelHeight;
            _isFitMode = true;
            ResetPan();
            FitImageToViewport();
        }

        private void UpdateImageAvailability()
        {
            if (_card == null)
            {
                return;
            }

            if (!_state.SelectAvailableSide(
                    _state.CurrentSide,
                    HasImage(_card.FrontImageData),
                    HasImage(_card.BackImageData)))
            {
                Close();
                return;
            }

            UpdateSideButtons();
            LoadCurrentImage();
        }

        private void UpdateSideButtons()
        {
            if (_card == null)
            {
                return;
            }

            FrontSideButton.IsEnabled = HasImage(_card.FrontImageData);
            BackSideButton.IsEnabled = HasImage(_card.BackImageData);
            FrontSideButton.IsChecked = _state.CurrentSide == CardImageSide.Front;
            BackSideButton.IsChecked = _state.CurrentSide == CardImageSide.Back;
        }

        private void SelectSide(CardImageSide side)
        {
            if (_card == null || !_state.SelectAvailableSide(
                    side,
                    HasImage(_card.FrontImageData),
                    HasImage(_card.BackImageData)))
            {
                return;
            }

            UpdateSideButtons();
            LoadCurrentImage();
        }

        private void FitImageToViewport()
        {
            if (_bitmap == null || _bitmap.PixelWidth <= 0 || _bitmap.PixelHeight <= 0
                || ViewportGrid.ActualWidth <= 0 || ViewportGrid.ActualHeight <= 0)
            {
                return;
            }

            var availableWidth = Math.Max(1, ViewportGrid.ActualWidth - 32);
            var availableHeight = Math.Max(1, ViewportGrid.ActualHeight - 32);
            var fitZoom = Math.Min(availableWidth / _bitmap.PixelWidth, availableHeight / _bitmap.PixelHeight);
            _state.Fit(fitZoom);
            _isFitMode = true;
            ResetPan();
            ApplyTransform();
        }

        private void SetZoom(double zoomFactor, Point? anchor = null)
        {
            var previousZoom = _state.ZoomFactor;
            _state.SetZoom(zoomFactor);
            if (Math.Abs(previousZoom - _state.ZoomFactor) < 0.001)
            {
                return;
            }

            _isFitMode = false;
            if (anchor is Point point)
            {
                var centerX = ViewportGrid.ActualWidth / 2;
                var centerY = ViewportGrid.ActualHeight / 2;
                var imageX = (point.X - centerX - _translateX) / previousZoom;
                var imageY = (point.Y - centerY - _translateY) / previousZoom;
                _translateX = point.X - centerX - (imageX * _state.ZoomFactor);
                _translateY = point.Y - centerY - (imageY * _state.ZoomFactor);
            }

            ClampPan();
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (_bitmap != null)
            {
                var scaledWidth = _bitmap.PixelWidth * _state.ZoomFactor;
                var scaledHeight = _bitmap.PixelHeight * _state.ZoomFactor;

                // Canvas preserves the complete oversized image. The viewport clips only the
                // final visible area, so panning can reveal every part of the bitmap.
                ViewerImage.Width = scaledWidth;
                ViewerImage.Height = scaledHeight;
                Canvas.SetLeft(ViewerImage, ((ViewportGrid.ActualWidth - scaledWidth) / 2) + _translateX);
                Canvas.SetTop(ViewerImage, ((ViewportGrid.ActualHeight - scaledHeight) / 2) + _translateY);
            }
            ZoomText.Text = $"{Math.Round(_state.ZoomFactor * 100):0}%";
            ZoomOutButton.IsEnabled = _state.ZoomFactor > ImageViewerState.MinimumZoom;
            ZoomInButton.IsEnabled = _state.ZoomFactor < ImageViewerState.MaximumZoom;
        }

        private void ClampPan()
        {
            if (_bitmap == null)
            {
                ResetPan();
                return;
            }

            var maxX = Math.Max(0, ((_bitmap.PixelWidth * _state.ZoomFactor) - ViewportGrid.ActualWidth) / 2);
            var maxY = Math.Max(0, ((_bitmap.PixelHeight * _state.ZoomFactor) - ViewportGrid.ActualHeight) / 2);
            _translateX = Math.Clamp(_translateX, -maxX, maxX);
            _translateY = Math.Clamp(_translateY, -maxY, maxY);
        }

        private void ResetPan()
        {
            _translateX = 0;
            _translateY = 0;
        }

        private void ApplyAlwaysOnTop()
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = _state.IsAlwaysOnTop;
            }

            AlwaysOnTopButton.IsChecked = _state.IsAlwaysOnTop;
        }

        private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_card == null)
                {
                    return;
                }

                if (e.PropertyName is nameof(BusinessCard.FrontImageData) or nameof(BusinessCard.BackImageData))
                {
                    UpdateImageAvailability();
                }
                else if (e.PropertyName is nameof(BusinessCard.FullName) or nameof(BusinessCard.DisplayName))
                {
                    CardNameText.Text = _card.DisplayName;
                }
            });
        }

        private void OnFrontSideClicked(object sender, RoutedEventArgs e) => SelectSide(CardImageSide.Front);

        private void OnBackSideClicked(object sender, RoutedEventArgs e) => SelectSide(CardImageSide.Back);

        private void OnZoomOutClicked(object sender, RoutedEventArgs e) => SetZoom(_state.ZoomFactor - ImageViewerState.ZoomStep);

        private void OnZoomInClicked(object sender, RoutedEventArgs e) => SetZoom(_state.ZoomFactor + ImageViewerState.ZoomStep);

        private void OnActualSizeClicked(object sender, RoutedEventArgs e)
        {
            _state.ResetToActualSize();
            _isFitMode = false;
            ResetPan();
            ApplyTransform();
        }

        private void OnFitClicked(object sender, RoutedEventArgs e) => FitImageToViewport();

        private void OnAlwaysOnTopClicked(object sender, RoutedEventArgs e)
        {
            _state.IsAlwaysOnTop = AlwaysOnTopButton.IsChecked == true;
            ApplyAlwaysOnTop();
            AlwaysOnTopChanged?.Invoke(this, _state.IsAlwaysOnTop);
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewportGrid.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
            };

            if (_isFitMode)
            {
                FitImageToViewport();
            }
            else
            {
                ClampPan();
                ApplyTransform();
            }
        }

        private void OnViewportPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(ViewportGrid);
            SetZoom(
                _state.ZoomFactor + (point.Properties.MouseWheelDelta > 0
                    ? ImageViewerState.ZoomStep
                    : -ImageViewerState.ZoomStep),
                point.Position);
            e.Handled = true;
        }

        private void OnViewportPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(ViewportGrid);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            _isPanning = true;
            _lastPointerPosition = point.Position;
            ViewportGrid.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnViewportPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPanning)
            {
                return;
            }

            var position = e.GetCurrentPoint(ViewportGrid).Position;
            _translateX += position.X - _lastPointerPosition.X;
            _translateY += position.Y - _lastPointerPosition.Y;
            _lastPointerPosition = position;
            ClampPan();
            ApplyTransform();
            e.Handled = true;
        }

        private void OnViewportPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isPanning = false;
            ViewportGrid.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void OnViewportPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isPanning = false;
        }

        private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case VirtualKey.Add:
                    SetZoom(_state.ZoomFactor + ImageViewerState.ZoomStep);
                    e.Handled = true;
                    break;
                case VirtualKey.Subtract:
                    SetZoom(_state.ZoomFactor - ImageViewerState.ZoomStep);
                    e.Handled = true;
                    break;
                case VirtualKey.Number0:
                case VirtualKey.NumberPad0:
                    ResetPan();
                    SetZoom(1.0);
                    e.Handled = true;
                    break;
            }
        }

        private void OnLanguageChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateLocalizedText);
        }

        private void UpdateLocalizedText()
        {
            Title = _localizationService.GetString("View.ImageViewer.Title");
            FrontSideButton.Content = _localizationService.GetString("View.CardDetail.FrontSide");
            BackSideButton.Content = _localizationService.GetString("View.CardDetail.BackSide");
            ToolTipService.SetToolTip(ZoomOutButton, _localizationService.GetString("View.ImageViewer.ZoomOut"));
            ToolTipService.SetToolTip(ZoomInButton, _localizationService.GetString("View.ImageViewer.ZoomIn"));
            ToolTipService.SetToolTip(ActualSizeButton, _localizationService.GetString("View.ImageViewer.ActualSize"));
            ToolTipService.SetToolTip(FitButton, _localizationService.GetString("View.ImageViewer.Fit"));
            ToolTipService.SetToolTip(AlwaysOnTopButton, _localizationService.GetString("View.ImageViewer.AlwaysOnTop"));
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_card != null)
            {
                _card.PropertyChanged -= OnCardPropertyChanged;
                _card = null;
            }

            _localizationService.LanguageChanged -= OnLanguageChanged;
            Closed -= OnWindowClosed;
            ViewerClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}

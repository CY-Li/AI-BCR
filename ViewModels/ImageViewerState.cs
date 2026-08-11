using CommunityToolkit.Mvvm.ComponentModel;
using PlustekBCR.Models;

namespace PlustekBCR.ViewModels
{
    public partial class ImageViewerState : ObservableObject
    {
        public const double MinimumZoom = 0.25;
        public const double MaximumZoom = 4.0;
        public const double ZoomStep = 0.25;

        [ObservableProperty]
        public partial double ZoomFactor { get; private set; } = 1.0;

        [ObservableProperty]
        public partial CardImageSide CurrentSide { get; private set; } = CardImageSide.Front;

        [ObservableProperty]
        public partial bool IsAlwaysOnTop { get; set; } = true;

        public void ZoomIn() => SetZoom(ZoomFactor + ZoomStep);

        public void ZoomOut() => SetZoom(ZoomFactor - ZoomStep);

        public void ResetToActualSize() => SetZoom(1.0);

        public void Fit(double zoomFactor) => SetZoom(zoomFactor);

        public void SetZoom(double zoomFactor)
        {
            ZoomFactor = Math.Clamp(zoomFactor, MinimumZoom, MaximumZoom);
        }

        public bool SelectAvailableSide(CardImageSide requestedSide, bool hasFrontImage, bool hasBackImage)
        {
            if (!hasFrontImage && !hasBackImage)
            {
                return false;
            }

            CurrentSide = requestedSide switch
            {
                CardImageSide.Front when hasFrontImage => CardImageSide.Front,
                CardImageSide.Back when hasBackImage => CardImageSide.Back,
                _ when hasFrontImage => CardImageSide.Front,
                _ => CardImageSide.Back
            };

            return true;
        }
    }
}

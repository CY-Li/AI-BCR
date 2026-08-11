using PlustekBCR.Models;
using PlustekBCR.ViewModels;
using Xunit;

namespace PlustekBCR.Tests;

public sealed class ImageViewerStateTests
{
    [Fact]
    public void Zoom_IsClampedToSupportedRange()
    {
        var state = new ImageViewerState();

        state.SetZoom(0.01);
        Assert.Equal(ImageViewerState.MinimumZoom, state.ZoomFactor);

        state.SetZoom(10);
        Assert.Equal(ImageViewerState.MaximumZoom, state.ZoomFactor);
    }

    [Fact]
    public void ZoomButtons_UseQuarterStepAndActualSizeResets()
    {
        var state = new ImageViewerState();

        state.ZoomIn();
        Assert.Equal(1.25, state.ZoomFactor);

        state.ZoomOut();
        Assert.Equal(1.0, state.ZoomFactor);

        state.SetZoom(2.5);
        state.ResetToActualSize();
        Assert.Equal(1.0, state.ZoomFactor);
    }

    [Fact]
    public void MissingRequestedSide_FallsBackToAvailableImage()
    {
        var state = new ImageViewerState();

        Assert.True(state.SelectAvailableSide(CardImageSide.Back, hasFrontImage: true, hasBackImage: false));
        Assert.Equal(CardImageSide.Front, state.CurrentSide);

        Assert.True(state.SelectAvailableSide(CardImageSide.Front, hasFrontImage: false, hasBackImage: true));
        Assert.Equal(CardImageSide.Back, state.CurrentSide);
    }

    [Fact]
    public void NoImages_ReturnsFalseWithoutChangingCurrentSide()
    {
        var state = new ImageViewerState();
        state.SelectAvailableSide(CardImageSide.Back, hasFrontImage: false, hasBackImage: true);

        Assert.False(state.SelectAvailableSide(CardImageSide.Front, hasFrontImage: false, hasBackImage: false));
        Assert.Equal(CardImageSide.Back, state.CurrentSide);
    }
}

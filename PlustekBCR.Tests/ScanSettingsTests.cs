using PlustekBCR.Models;
using Xunit;

namespace PlustekBCR.Tests;

public sealed class ScanSettingsTests
{
    [Fact]
    public void Default_UsesBalancedResolutionColorAndAutomaticCorrections()
    {
        var settings = ScanSettings.Default;

        Assert.Equal(ScanResolution.Better, settings.Resolution);
        Assert.Equal(300, (int)settings.Resolution);
        Assert.Equal(ScanColorMode.Color, settings.ColorMode);
        Assert.True(settings.AutoCrop);
        Assert.True(settings.AutoDeskew);
        Assert.True(settings.AutoRotate);
    }

    [Fact]
    public void Clone_CreatesIndependentSettingsCopy()
    {
        var original = new ScanSettings
        {
            Resolution = ScanResolution.Excellent,
            ColorMode = ScanColorMode.Grayscale,
            AutoCrop = false,
            AutoDeskew = true,
            AutoRotate = false
        };

        var clone = original.Clone();
        clone.Resolution = ScanResolution.Good;

        Assert.Equal(ScanResolution.Excellent, original.Resolution);
        Assert.Equal(ScanColorMode.Grayscale, clone.ColorMode);
        Assert.False(clone.AutoCrop);
        Assert.True(clone.AutoDeskew);
        Assert.False(clone.AutoRotate);
    }
}

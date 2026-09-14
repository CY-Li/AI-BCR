namespace PlustekBCR.Models
{
    public enum ScanResolution
    {
        Good = 200,
        Better = 300,
        Excellent = 600
    }

    public enum ScanColorMode
    {
        Color,
        Grayscale,
        BlackAndWhite
    }

    public sealed class ScanSettings
    {
        public ScanResolution Resolution { get; set; } = ScanResolution.Better;
        public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;
        public bool AutoCrop { get; set; } = true;
        public bool AutoDeskew { get; set; } = true;
        public bool AutoRotate { get; set; } = true;

        public static ScanSettings Default => new();

        public ScanSettings Clone() => new()
        {
            Resolution = Resolution,
            ColorMode = ColorMode,
            AutoCrop = AutoCrop,
            AutoDeskew = AutoDeskew,
            AutoRotate = AutoRotate
        };
    }
}

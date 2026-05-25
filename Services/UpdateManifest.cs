namespace PlustekBCR.Services;

public class UpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

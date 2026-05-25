namespace PlustekBCR.Services;

public class UpdateOptions
{
    public bool Enabled { get; set; } = false;
    public string ManifestUrl { get; set; } = string.Empty;
    public int CheckTimeoutSeconds { get; set; } = 5;
}

namespace PlustekBCR.Models.Plustek
{
    public class PlustekConsoleOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string EscanServiceId { get; set; } = string.Empty;
        public int PollIntervalMs { get; set; } = 1500;
        public int ResultTimeoutSeconds { get; set; } = 45;
    }
}

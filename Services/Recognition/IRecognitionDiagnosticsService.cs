namespace PlustekBCR.Services.Recognition
{
    public interface IRecognitionDiagnosticsService
    {
        Task LogAsync(string category, string message, CancellationToken cancellationToken = default);
    }
}

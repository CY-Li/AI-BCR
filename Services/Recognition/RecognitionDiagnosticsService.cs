using System.Text;

namespace PlustekBCR.Services.Recognition
{
    public class RecognitionDiagnosticsService : IRecognitionDiagnosticsService
    {
        private readonly string _logPath;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public RecognitionDiagnosticsService()
        {
            _logPath = Path.Combine(AppContext.BaseDirectory, "recognition_log.txt");
        }

        public async Task LogAsync(string category, string message, CancellationToken cancellationToken = default)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {message}{Environment.NewLine}";

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8, cancellationToken);
            }
            catch
            {
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}

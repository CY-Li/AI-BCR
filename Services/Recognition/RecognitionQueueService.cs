using CommunityToolkit.Mvvm.Messaging;
using PlustekBCR.Models;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Recognition
{
    public class RecognitionQueueService : IRecognitionQueueService
    {
        private readonly IBusinessCardRecognitionService _businessCardRecognitionService;
        private readonly IApplicationSettingsService _applicationSettingsService;
        private readonly IRecognitionDiagnosticsService _diagnosticsService;
        private readonly SemaphoreSlim _queueSemaphore = new(1);
        private int _configurationWarningShown;

        public RecognitionQueueService(
            IBusinessCardRecognitionService businessCardRecognitionService,
            IApplicationSettingsService applicationSettingsService,
            IRecognitionDiagnosticsService diagnosticsService)
        {
            _businessCardRecognitionService = businessCardRecognitionService;
            _applicationSettingsService = applicationSettingsService;
            _diagnosticsService = diagnosticsService;
        }

        public Task EnqueueAsync(BusinessCard card, CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new[] { card }, cancellationToken);
        }

        public async Task EnqueueAsync(IEnumerable<BusinessCard> cards, CancellationToken cancellationToken = default)
        {
            var tasks = cards.Select(card => ProcessCardAsync(card, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessCardAsync(BusinessCard card, CancellationToken cancellationToken)
        {
            if (!_applicationSettingsService.IsAiEnabled)
            {
                await _diagnosticsService.LogAsync("RecognitionQueue", $"Card={card.Id} skipped because AI is turned off. Market={card.MarketCode}.", cancellationToken);
                await UpdateCardOnUiThreadAsync(card, () =>
                {
                    card.Status = ProcessingStatus.Manual;
                    AppendRecognitionNote(card, "AI is turned off. Card kept for manual entry.");
                });
                return;
            }

            var semaphoreEntered = false;
            var countIncremented = false;
            try
            {
                await UpdateCardOnUiThreadAsync(card, () =>
                {
                    card.Status = ProcessingStatus.Pending;
                });

                await _diagnosticsService.LogAsync("RecognitionQueue", $"Card={card.Id} queued. Market={card.MarketCode}. AutoScan={card.IsAutoScanSession}.", cancellationToken);
                await _queueSemaphore.WaitAsync(cancellationToken);
                semaphoreEntered = true;

                await UpdateCardOnUiThreadAsync(card, () =>
                {
                    card.Status = ProcessingStatus.Recognizing;
                });

                if (card.IsAutoScanSession)
                {
                    WeakReferenceMessenger.Default.Send(new AutoScanRecognitionCountChangedMessage(1));
                    countIncremented = true;
                }

                await _businessCardRecognitionService.RecognizeAsync(card, cancellationToken);
                await _diagnosticsService.LogAsync("RecognitionQueue", $"Card={card.Id} recognition completed successfully. Market={card.MarketCode}.", cancellationToken);

                await UpdateCardOnUiThreadAsync(card, () =>
                {
                    card.Status = ProcessingStatus.Done;
                    AppendRecognitionNote(card, "Automatically recognized and parsed by Document Agent.");
                });
            }
            catch (Exception ex)
            {
                await _diagnosticsService.LogAsync("FallbackToManual", $"Card={card.Id} market={card.MarketCode} recognition failed: {ex}", cancellationToken);
                await UpdateCardOnUiThreadAsync(card, () =>
                {
                    card.Status = ProcessingStatus.Manual;
                    AppendRecognitionNote(card, GetManualFallbackMessage(card, ex));
                });

                if (IsConfigurationException(ex) && Interlocked.Exchange(ref _configurationWarningShown, 1) == 0)
                {
                    WeakReferenceMessenger.Default.Send(new RecognitionWarningMessage(
                        "AI recognition is not configured",
                        $"Import entered the shared recognition flow, but Plustek Console settings for {card.MarketCode} market are incomplete. Please fill BaseUrl, ApiKey, ApiSecret, and EscanServiceId in appsettings.json, then restart the app."));
                }
            }
            finally
            {
                if (countIncremented)
                {
                    WeakReferenceMessenger.Default.Send(new AutoScanRecognitionCountChangedMessage(-1));
                }

                if (semaphoreEntered)
                {
                    _queueSemaphore.Release();
                }
            }
        }

        private static bool IsConfigurationException(Exception ex)
        {
            return ex is RecognitionException recognitionException
                && recognitionException.FailureKind == RecognitionFailureKind.Configuration;
        }

        private static void AppendRecognitionNote(BusinessCard card, string content)
        {
            var notes = new List<Note>(card.Notes);
            notes.Insert(0, new Note { Content = content });
            card.Notes = notes;
        }

        private static string GetManualFallbackMessage(BusinessCard card, Exception exception)
        {
            if (exception is RecognitionException recognitionException)
            {
                return recognitionException.FailureKind switch
                {
                    RecognitionFailureKind.Configuration => $"AI recognition is not configured for {card.MarketCode} market.",
                    RecognitionFailureKind.JobFailed => "AI recognition failed at service side.",
                    RecognitionFailureKind.UnreadableResult => "AI returned an unreadable result.",
                    RecognitionFailureKind.UiUpdateFailed => "AI result received but UI update failed.",
                    RecognitionFailureKind.InvalidInput => recognitionException.Message,
                    RecognitionFailureKind.TimedOut => "AI recognition timed out.",
                    RecognitionFailureKind.Network => "AI recognition failed due to a temporary network problem.",
                    _ => "AI recognition fallback to manual mode."
                };
            }

            if (exception is TimeoutException)
            {
                return "AI recognition timed out.";
            }

            if (exception is HttpRequestException || exception is TaskCanceledException)
            {
                return "AI recognition failed due to a temporary network problem.";
            }

            return "AI recognition fallback to manual mode.";
        }

        private static Task UpdateCardOnUiThreadAsync(BusinessCard card, Action updateAction)
        {
            var tcs = new TaskCompletionSource<object?>();
            var dispatcher = App.Window?.DispatcherQueue;
            if (dispatcher == null)
            {
                updateAction();
                tcs.SetResult(null);
                return tcs.Task;
            }

            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    updateAction();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}

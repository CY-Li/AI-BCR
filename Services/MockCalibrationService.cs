namespace PlustekBCR.Services;

public sealed class MockCalibrationService : ICalibrationService
{
    public async Task CalibrateAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
    }
}

namespace PlustekBCR.Services;

public interface ICalibrationService
{
    Task CalibrateAsync(CancellationToken cancellationToken);
}

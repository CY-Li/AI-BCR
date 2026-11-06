using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlustekBCR.Services;

namespace PlustekBCR.ViewModels;

public enum CalibrationState { Ready, Working, Success, Error }

public sealed class CalibrationViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _scanner;
    private readonly ICalibrationService _calibrationService;
    private readonly ILocalizationService _localization;
    private CancellationTokenSource? _operation;
    private CalibrationState _state = CalibrationState.Ready;
    private string _errorKey = "Settings.Calibration.Error.Generic";
    private bool _isDialogOpen;

    public CalibrationViewModel(MainViewModel scanner, ICalibrationService calibrationService, ILocalizationService localization)
    {
        _scanner = scanner;
        _calibrationService = calibrationService;
        _localization = localization;
        _scanner.PropertyChanged += OnScannerPropertyChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        OpenCommand = new RelayCommand(Open, () => CanOpen);
        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        CloseCommand = new RelayCommand(Close);
    }

    public IRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public bool IsDialogOpen { get => _isDialogOpen; private set => SetProperty(ref _isDialogOpen, value); }
    public bool IsBackgroundEnabled => !IsDialogOpen;
    public CalibrationState State { get => _state; private set { if (SetProperty(ref _state, value)) Refresh(); } }
    public bool IsReady => State == CalibrationState.Ready;
    public bool IsWorking => State == CalibrationState.Working;
    public bool IsSuccess => State == CalibrationState.Success;
    public bool IsError => State == CalibrationState.Error;
    public bool ShowStartAction => IsReady || IsError;
    public bool CanOpen => _scanner.IsScannerConnected && !_scanner.IsAutoScanMode && !IsDialogOpen;
    public bool CanStart => IsDialogOpen && (IsReady || IsError) && _scanner.IsScannerConnected && !_scanner.IsAutoScanMode;
    public string AvailabilityText => !_scanner.IsScannerConnected
        ? _localization.GetString("Settings.Calibration.Disconnected")
        : _scanner.IsAutoScanMode
            ? _localization.GetString("Settings.Calibration.Busy")
            : _localization.GetString("Settings.Calibration.Connected");
    public string ErrorText => _localization.GetString(_errorKey);
    public string StartText => _localization.GetString(IsError ? "Button.Retry" : "Settings.Calibration.Start");

    private void Open()
    {
        if (!CanOpen) return;
        State = CalibrationState.Ready;
        IsDialogOpen = true;
        OnPropertyChanged(nameof(IsBackgroundEnabled));
        Refresh();
    }

    private async Task StartAsync()
    {
        if (!CanStart) return;
        _operation = new CancellationTokenSource();
        var operation = _operation;
        State = CalibrationState.Working;
        try
        {
            await _calibrationService.CalibrateAsync(operation.Token);
            if (operation.Token.IsCancellationRequested || !IsDialogOpen) return;
            if (!_scanner.IsScannerConnected || _scanner.IsAutoScanMode)
            {
                SetError(!_scanner.IsScannerConnected ? "Settings.Calibration.Error.Disconnected" : "Settings.Calibration.Error.Busy");
                return;
            }
            State = CalibrationState.Success;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            // Closing the dialog or losing the scanner ends only this mock operation.
        }
        catch (Exception)
        {
            if (IsDialogOpen) SetError("Settings.Calibration.Error.Generic");
        }
        finally
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            operation.Dispose();
        }
    }

    private void SetError(string key)
    {
        _errorKey = key;
        OnPropertyChanged(nameof(ErrorText));
        State = CalibrationState.Error;
    }

    private void OnScannerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsScannerConnected) && e.PropertyName != nameof(MainViewModel.IsAutoScanMode)) return;
        if (IsWorking && (!_scanner.IsScannerConnected || _scanner.IsAutoScanMode))
        {
            _operation?.Cancel();
            SetError(!_scanner.IsScannerConnected ? "Settings.Calibration.Error.Disconnected" : "Settings.Calibration.Error.Busy");
        }
        Refresh();
    }

    private void OnLanguageChanged() => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(ShowStartAction));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(StartText));
        OpenCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
    }

    private void Close()
    {
        _operation?.Cancel();
        IsDialogOpen = false;
        OnPropertyChanged(nameof(IsBackgroundEnabled));
        State = CalibrationState.Ready;
        Refresh();
    }

    public void Dispose()
    {
        Close();
        _scanner.PropertyChanged -= OnScannerPropertyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}

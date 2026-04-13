using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Inventory;

public partial class InventoryScan : IDisposable
{
    [Inject] private InventoryService Inventory { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private enum ScanState { ScanningFirst, ScanningSecond, Done }

    private bool? _nfcSupported;
    private ScanState _state = ScanState.ScanningFirst;
    private InventoryIdentifyResult? _firstEntity;
    private InventoryIdentifyResult? _secondEntity;
    private InventoryActionResult? _result;
    private string? _errorMessage;
    private string? _hintMessage;
    private bool _hintIsWarning;
    private bool _processing;
    private bool _scannerRunning;
    private bool _disposed;
    private DateTime _lastReadAt = DateTime.MinValue;
    private DotNetObjectReference<InventoryScan>? _dotNetRef;

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _nfcSupported = await Nfc.CheckSupportAsync();
        if (_nfcSupported == true)
            await StartScannerAsync();
    }

    private async Task StartScannerAsync()
    {
        _dotNetRef?.Dispose();
        _dotNetRef = DotNetObjectReference.Create(this);
        await Nfc.StartReadAsync(_dotNetRef);
        _scannerRunning = true;
    }

    private async Task PauseScannerAsync()
    {
        if (!_scannerRunning || _state == ScanState.Done)
            return;
        await Nfc.StopReadAsync();
        _scannerRunning = false;
        SetHint(L["inventory.scan.paused"]);
    }

    private async Task ResumeScannerAsync()
    {
        if (_scannerRunning || _state == ScanState.Done)
            return;
        await StartScannerAsync();
        SetHint(L["inventory.scan.resumed"]);
    }

    private async Task RestartAsync()
    {
        _firstEntity = null;
        _secondEntity = null;
        _result = null;
        _errorMessage = null;
        _hintMessage = null;
        _hintIsWarning = false;
        _processing = false;
        _scannerRunning = false;
        _state = ScanState.ScanningFirst;
        StateHasChanged();
        await StartScannerAsync();
    }

    [JSInvokable]
    public void OnScanStarted() { }

    [JSInvokable]
    public async Task OnTagRead(string json, string serialNumber)
    {
        if (_disposed || _processing || _state == ScanState.Done || !_scannerRunning)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastReadAt).TotalMilliseconds < 700)
            return;
        _lastReadAt = now;

        _processing = true;
        _errorMessage = null;

        try
        {
            var identified = await Inventory.IdentifyAsync(
                new InventoryIdentifyRequest { JsonPayload = json, SerialNumber = serialNumber });

            if (identified == null)
            {
                SetHint(L["inventory.scan.identify.error"], isWarning: true);
                return;
            }

            if (_state == ScanState.ScanningFirst)
            {
                if (identified == null || identified.EntityType == "unknown")
                {
                    SetHint(L["inventory.scan.unknown"], isWarning: true);
                    return;
                }

                _firstEntity = identified;
                _state = ScanState.ScanningSecond;
                SetHint(L["inventory.scan.first.ok"]);
                StateHasChanged();
            }
            else if (_state == ScanState.ScanningSecond && _firstEntity != null)
            {
                if (identified.EntityType == "unknown")
                {
                    SetHint(L["inventory.scan.unknown"], isWarning: true);
                    return;
                }

                if (identified.EntityType == _firstEntity.EntityType && identified.EntityId == _firstEntity.EntityId)
                {
                    SetHint(L["inventory.scan.same"], isWarning: true);
                    return;
                }

                _secondEntity = identified;
                await ExecuteActionAndContinueAsync();
            }
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task ExecuteActionAndContinueAsync()
    {
        if (_firstEntity == null || _secondEntity == null)
            return;

        await Nfc.StopReadAsync();
        _scannerRunning = false;

        _errorMessage = null;
        try
        {
            _result = await Inventory.PerformActionAsync(
                new InventoryActionRequest { First = _firstEntity, Second = _secondEntity });
            _state = ScanState.Done;
            _hintMessage = null;
            _hintIsWarning = false;
            StateHasChanged();

            await Task.Delay(1000);

            if (!_disposed)
                await RestartAsync();
        }
        catch (Exception)
        {
            _state = ScanState.Done;
            _result = new InventoryActionResult
            {
                Success = false,
                Description = L["inventory.scan.identify.error"]
            };
            StateHasChanged();

            await Task.Delay(1000);

            if (!_disposed)
                await RestartAsync();
        }
    }

    [JSInvokable]
    public async Task OnReadError(string message)
    {
        if (_disposed || _state == ScanState.Done)
            return;
        _errorMessage = message;
        StateHasChanged();
        await Task.Delay(1500);
        if (!_disposed && _state != ScanState.Done && _scannerRunning)
        {
            _errorMessage = null;
            await StartScannerAsync();
        }
    }

    [JSInvokable] public void OnWriteSuccess() { }
    [JSInvokable] public void OnWriteError(string message) { }

    public void Dispose()
    {
        _disposed = true;
        try { _ = Nfc.StopReadAsync(); } catch { }
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    private void SetHint(string message, bool isWarning = false)
    {
        _hintMessage = message;
        _hintIsWarning = isWarning;
    }

    private string GetProgressLabel() => _state switch
    {
        ScanState.ScanningFirst => L["inventory.progress.first"],
        ScanState.ScanningSecond => L["inventory.progress.second"],
        _ => string.Empty
    };

    private static string GetIcon(string entityType) => entityType switch
    {
        "spool" => "bi bi-archive",
        "printer" => "bi bi-printer",
        "storage" => "bi bi-box",
        _ => "bi bi-question-circle"
    };
}

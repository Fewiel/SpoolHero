using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Inventory;

/// <summary>
/// Two-step move: scan a spool, then the place it goes to (printer, storage, dryer) – or the other way round.
/// The scanner keeps running so many spools can be moved in a row; every move is listed in the session history.
/// </summary>
public partial class InventoryScan : IDisposable
{
    [Inject] private InventoryService Inventory { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private enum ScanState { ScanningFirst, ScanningSecond, Done }

    private sealed record HistoryEntry(DateTime At, string SpoolName, string LocationName, bool Success, string Description, string? ColorHex, decimal Percent);

    private bool? _nfcSupported;
    private ScanState _state = ScanState.ScanningFirst;
    private InventoryIdentifyResult? _firstEntity;
    private SpoolDto? _firstSpool;
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
    private readonly List<HistoryEntry> _history = [];

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _nfcSupported = await Nfc.CheckSupportAsync();
        if (_nfcSupported == true)
            await StartScannerAsync();
    }

    private string StepClass(int step) => step switch
    {
        1 when _firstEntity != null => "inv-step-done",
        1 => "inv-step-active",
        2 when _state == ScanState.Done => _result?.Success == true ? "inv-step-done" : "inv-step-fail",
        2 when _state == ScanState.ScanningSecond => "inv-step-active",
        _ => "inv-step-waiting"
    };

    private async Task StartScannerAsync()
    {
        _dotNetRef?.Dispose();
        _dotNetRef = DotNetObjectReference.Create(this);
        try
        {
            await Nfc.StartReadAsync(_dotNetRef);
            _scannerRunning = true;
        }
        catch (JSException ex)
        {
            _errorMessage = ex.Message;
            _scannerRunning = false;
        }
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
        _firstSpool = null;
        _secondEntity = null;
        _result = null;
        _errorMessage = null;
        _hintMessage = null;
        _hintIsWarning = false;
        _processing = false;
        _state = ScanState.ScanningFirst;
        StateHasChanged();
        if (_nfcSupported == true && !_disposed)
        {
            _scannerRunning = false;
            await StartScannerAsync();
        }
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

        await ProcessTagAsync(json, serialNumber);
    }

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        if (_processing || _state == ScanState.Done)
            return;
        _errorMessage = null;
        try
        {
            await using var stream = e.File.OpenReadStream(maxAllowedSize: 64 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            string? json;
            if (e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || (bytes.Length > 0 && bytes[0] == (byte)'{'))
                json = Encoding.UTF8.GetString(bytes);
            else
                json = (await Tags.DecodeAsync(new TagDecodeRequest { Base64 = Convert.ToBase64String(bytes) }))?.RawJson;

            if (string.IsNullOrWhiteSpace(json))
            {
                SetHint(L["tag.read.file.invalid"], isWarning: true);
                return;
            }
            await ProcessTagAsync(json, string.Empty);
        }
        catch (Exception)
        {
            SetHint(L["tag.read.file.invalid"], isWarning: true);
        }
    }

    private async Task ProcessTagAsync(string json, string serialNumber)
    {
        _processing = true;
        _errorMessage = null;

        try
        {
            var identified = await Inventory.IdentifyAsync(
                new InventoryIdentifyRequest
                {
                    JsonPayload = string.IsNullOrWhiteSpace(json) ? null : json,
                    SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber
                });

            if (identified == null)
            {
                SetHint(L["inventory.scan.identify.error"], isWarning: true);
                return;
            }

            if (identified.EntityType == "unknown")
            {
                SetHint(L["inventory.scan.unknown"], isWarning: true);
                return;
            }

            if (_state == ScanState.ScanningFirst)
            {
                _firstEntity = identified;
                _firstSpool = identified.EntityType == "spool" ? await TryGetSpoolAsync(identified.EntityId) : null;
                _state = ScanState.ScanningSecond;
                SetHint(identified.EntityType == "spool" ? L["inventory.scan.first.ok"] : L["inventory.scan.first.ok.location"]);
            }
            else if (_state == ScanState.ScanningSecond && _firstEntity != null)
            {
                if (identified.EntityType == _firstEntity.EntityType && identified.EntityId == _firstEntity.EntityId)
                {
                    SetHint(L["inventory.scan.same"], isWarning: true);
                    return;
                }

                var firstIsSpool = _firstEntity.EntityType == "spool";
                var secondIsSpool = identified.EntityType == "spool";
                if (firstIsSpool == secondIsSpool)
                {
                    // Two spools or two places: the newer scan becomes step 1.
                    _firstEntity = identified;
                    _firstSpool = secondIsSpool ? await TryGetSpoolAsync(identified.EntityId) : null;
                    SetHint(secondIsSpool ? L["inventory.scan.replaced.spool"] : L["inventory.scan.replaced.location"], isWarning: true);
                    return;
                }

                _secondEntity = identified;
                await ExecuteActionAndContinueAsync();
            }
        }
        catch (Exception)
        {
            SetHint(L["inventory.scan.identify.error"], isWarning: true);
        }
        finally
        {
            _processing = false;
            StateHasChanged();
        }
    }

    private async Task<SpoolDto?> TryGetSpoolAsync(Guid id)
    {
        try { return await Spools.GetByIdAsync(id); }
        catch (HttpRequestException) { return null; }
    }

    private async Task ExecuteActionAndContinueAsync()
    {
        if (_firstEntity == null || _secondEntity == null)
            return;

        if (_scannerRunning)
        {
            await Nfc.StopReadAsync();
            _scannerRunning = false;
        }

        var spoolEntity = _firstEntity.EntityType == "spool" ? _firstEntity : _secondEntity;
        var locationEntity = _firstEntity.EntityType == "spool" ? _secondEntity : _firstEntity;
        var spool = _firstSpool ?? await TryGetSpoolAsync(spoolEntity.EntityId);

        try
        {
            _result = await Inventory.PerformActionAsync(
                new InventoryActionRequest { First = _firstEntity, Second = _secondEntity })
                ?? new InventoryActionResult { Success = false, Description = L["inventory.scan.identify.error"] };
        }
        catch (Exception)
        {
            _result = new InventoryActionResult { Success = false, Description = L["inventory.scan.identify.error"] };
        }

        if (_result.Success)
            _result.Description = L.Format("inventory.result.moved", spoolEntity.EntityName, locationEntity.EntityName);

        _history.Add(new HistoryEntry(DateTime.Now, spoolEntity.EntityName, locationEntity.EntityName, _result.Success,
            _result.Description, spool?.MaterialColorHex, spool?.RemainingPercent ?? 100));

        _state = ScanState.Done;
        _hintMessage = null;
        _hintIsWarning = false;
        StateHasChanged();

        await Task.Delay(1800);

        if (!_disposed)
            await RestartAsync();
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

    private static string GetIcon(string entityType) => entityType switch
    {
        "spool" => "bi-record-circle",
        "printer" => "bi-printer",
        "storage" => "bi-box-seam",
        "dryer" => "bi-thermometer-sun",
        _ => "bi-question-circle"
    };
}

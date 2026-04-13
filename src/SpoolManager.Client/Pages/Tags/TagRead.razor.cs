using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Tags;

public partial class TagRead : IDisposable
{
    [Inject] private InventoryService Inventory { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _nfcSupported;
    private bool _scanning;
    private string? _error;
    private InventoryIdentifyResult? _result;
    private DotNetObjectReference<TagRead>? _objRef;

    protected override async Task OnInitializedAsync()
    {
        _nfcSupported = await Nfc.CheckSupportAsync();
    }

    private async Task ScanNfcAsync()
    {
        _scanning = true;
        _error = null;
        _result = null;
        try
        {
            _objRef?.Dispose();
            _objRef = DotNetObjectReference.Create(this);
            await Nfc.StartReadAsync(_objRef);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _scanning = false;
        }
    }

    private async Task StopScanAsync()
    {
        await Nfc.StopReadAsync();
        _scanning = false;
    }

    [JSInvokable]
    public void OnScanStarted() { }

    [JSInvokable]
    public void OnReadError(string error)
    {
        _error = error;
        _scanning = false;
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnTagRead(string json, string serialNumber)
    {
        _scanning = false;
        _error = null;
        _result = await Inventory.IdentifyAsync(new InventoryIdentifyRequest
        {
            JsonPayload = !string.IsNullOrWhiteSpace(json) ? json : null,
            SerialNumber = !string.IsNullOrWhiteSpace(serialNumber) ? serialNumber : null
        });
        StateHasChanged();
    }

    public void Dispose() => _objRef?.Dispose();
}

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;

namespace SpoolManager.Client.Components;

/// <summary>
/// Scans NFC tags until the tag of <see cref="Spool"/> is found – helps to pick the right spool from a shelf.
/// A tag matches by its UID (if stored on the spool) or by the spool id written into the OpenSpool JSON.
/// </summary>
public partial class FindSpoolModal : IAsyncDisposable
{
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    [Parameter, EditorRequired] public SpoolDto Spool { get; set; } = default!;

    private enum FindState { Scanning, Mismatch, Found }

    private bool _visible;
    private FindState _state = FindState.Scanning;
    private DotNetObjectReference<FindSpoolModal>? _ref;
    private CancellationTokenSource? _resetCts;

    public async Task OpenAsync()
    {
        _state = FindState.Scanning;
        _visible = true;
        StateHasChanged();

        _ref ??= DotNetObjectReference.Create(this);
        await Nfc.StartReadAsync(_ref);
    }

    [JSInvokable]
    public void OnScanStarted() { }

    [JSInvokable]
    public async Task OnTagRead(string? json, string? serialNumber)
    {
        _resetCts?.Cancel();

        if (IsMatch(json, serialNumber))
        {
            _state = FindState.Found;
            await InvokeAsync(StateHasChanged);
            await Nfc.StopReadAsync();
            await Task.Delay(2000);
            await CloseAsync();
            return;
        }

        _state = FindState.Mismatch;
        await InvokeAsync(StateHasChanged);

        _resetCts = new CancellationTokenSource();
        var token = _resetCts.Token;
        try
        {
            await Task.Delay(2000, token);
            _state = FindState.Scanning;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException) { }
    }

    [JSInvokable]
    public void OnReadError(string message)
    {
        _state = FindState.Scanning;
        _ = InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnWriteSuccess() { }

    [JSInvokable]
    public void OnWriteError(string message) { }

    private bool IsMatch(string? json, string? serialNumber)
    {
        var expectedUid = NormalizeUid(Spool.RfidTagUid);
        if (expectedUid.Length > 0 && NormalizeUid(serialNumber) == expectedUid)
            return true;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("_sm_spool_id", out var id)
                && Guid.TryParse(id.GetString(), out var guid) && guid == Spool.Id)
                return true;
            if (Spool.SpoolmanId > 0 && root.TryGetProperty("spool_id", out var smId)
                && smId.ValueKind == JsonValueKind.Number && smId.GetInt32() == Spool.SpoolmanId)
                return true;
        }
        catch (JsonException) { }

        return false;
    }

    private static string NormalizeUid(string? uid) =>
        (uid ?? string.Empty).Trim().ToLowerInvariant().Replace(":", "").Replace("-", "").Replace(" ", "");

    private async Task CloseAsync()
    {
        _resetCts?.Cancel();
        await Nfc.StopReadAsync();
        _state = FindState.Scanning;
        _visible = false;
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _resetCts?.Cancel();
        if (_visible)
            await Nfc.StopReadAsync();
        _ref?.Dispose();
    }
}

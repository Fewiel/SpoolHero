using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Tags;
using Timer = System.Timers.Timer;

namespace SpoolManager.Client.Pages.Spools;

public partial class SpoolDetail : IDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private SpoolDto? _spool;
    private bool _showRemainingForm;
    private decimal _remainingGrams;
    private decimal _remainingPct;
    private bool _nfcSupported;
    private bool _writingNfc;
    private string? _nfcMessage;
    private bool _nfcSuccess;
    private string? _tagJson;
    private Timer? _clockTimer;

    [JSInvokable] public void OnWriteSuccess() { _nfcMessage = L["tag.write.success"]; _nfcSuccess = true; _writingNfc = false; StateHasChanged(); }
    [JSInvokable] public void OnWriteError(string message) { _nfcMessage = message; _nfcSuccess = false; _writingNfc = false; StateHasChanged(); }
    [JSInvokable] public void OnScanStarted() { }
    [JSInvokable] public void OnTagRead(string json, string serialNumber) { }
    [JSInvokable] public void OnReadError(string message) { }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _spool = await Spools.GetByIdAsync(Id);
        if (_spool != null)
        {
            _remainingGrams = _spool.RemainingWeightGrams;
            _remainingPct = _spool.RemainingPercent;
        }
        _nfcSupported = await Nfc.CheckSupportAsync();
        _loading = false;
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
    }

    private async Task UpdateRemainingAsync()
    {
        await Spools.UpdateRemainingAsync(Id, new UpdateRemainingRequest { RemainingWeightGrams = _remainingGrams, RemainingPercent = _remainingPct });
        _spool = await Spools.GetByIdAsync(Id);
        _showRemainingForm = false;
    }

    private async Task MarkOpenedAsync() { await Spools.MarkOpenedAsync(Id); _spool = await Spools.GetByIdAsync(Id); }
    private async Task MarkDriedAsync() { await Spools.MarkDriedAsync(Id); _spool = await Spools.GetByIdAsync(Id); }
    private async Task MarkRepackagedAsync() { await Spools.MarkRepackagedAsync(Id); _spool = await Spools.GetByIdAsync(Id); }
    private async Task MarkReopenedAsync() { await Spools.MarkReopenedAsync(Id); _spool = await Spools.GetByIdAsync(Id); }
    private async Task MarkConsumedAsync() { await Spools.MarkConsumedAsync(Id); _spool = await Spools.GetByIdAsync(Id); }

    private async Task WriteNfcAsync()
    {
        _writingNfc = true;
        _nfcMessage = null;
        _tagJson = null;
        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = Id });
        if (encoded?.JsonPayload == null)
        {
            _nfcMessage = L["common.error"];
            _nfcSuccess = false;
            _writingNfc = false;
            return;
        }
        _tagJson = encoded.JsonPayload;
        await Nfc.WriteAsync(encoded.JsonPayload, DotNetObjectReference.Create(this));
    }

    private static string GetBarClass(decimal p) => p >= 50 ? "high" : p >= 20 ? "medium" : "low";

    private static (double elapsed, double total, bool done) GetDryingState(SpoolDto s)
    {
        if (s.DriedAt == null || !s.MaterialDryTimeHours.HasValue || s.MaterialDryTimeHours <= 0)
            return (0, 0, false);
        var elapsed = (DateTime.UtcNow - s.DriedAt.Value.ToUniversalTime()).TotalHours;
        var total = (double)s.MaterialDryTimeHours.Value;
        return (Math.Round(elapsed, 1), total, elapsed >= total);
    }

    public void Dispose() => _clockTimer?.Dispose();
}

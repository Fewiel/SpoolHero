using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Tags;
using Timer = System.Timers.Timer;

namespace SpoolManager.Client.Pages.Spools;

public partial class SpoolList : IDisposable
{
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private List<SpoolDto> _spools = [];
    private string _search = string.Empty;
    private string _filter = "active";
    private string _owner = string.Empty;
    private List<string> _owners = [];
    private bool _grouped;
    private SpoolDto? _deleteTarget;
    private Timer? _searchTimer;
    private Timer? _clockTimer;

    private bool _nfcAvailable;
    private SpoolDto? _nfcTarget;
    private bool _nfcWriting;
    private bool _nfcSuccess;
    private string? _nfcMessage;
    private Guid? _jsonTarget;
    private string? _jsonPayload;
    private bool _jsonCopied;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [JSInvokable]
    public void OnWriteSuccess()
    {
        _nfcMessage = L["tag.write.success"];
        _nfcSuccess = true;
        _nfcWriting = false;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnWriteError(string message)
    {
        _nfcMessage = message;
        _nfcSuccess = false;
        _nfcWriting = false;
        StateHasChanged();
    }

    [JSInvokable] public void OnScanStarted() { }
    [JSInvokable] public void OnTagRead(string json, string serialNumber) { }
    [JSInvokable] public void OnReadError(string message) { }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _grouped = Nav.Uri.Contains("view=materials", StringComparison.OrdinalIgnoreCase);
        _owners = await Spools.GetOwnersAsync() ?? [];
        await LoadAsync();
        _nfcAvailable = await Nfc.CheckSupportAsync();
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        bool? consumed = _filter == "active" ? false : _filter == "consumed" ? true : null;
        _spools = await Spools.GetAllAsync(consumed: consumed, search: string.IsNullOrWhiteSpace(_search) ? null : _search,
            owner: string.IsNullOrEmpty(_owner) ? null : _owner) ?? [];
        _loading = false;
    }

    private void OnSearch(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        _searchTimer?.Stop();
        _searchTimer = new System.Timers.Timer(300);
        _searchTimer.Elapsed += async (_, _) => { _searchTimer?.Stop(); await InvokeAsync(LoadAsync); };
        _searchTimer.Start();
    }

    private void SetGrouped(bool grouped)
    {
        _grouped = grouped;
        Nav.NavigateTo(grouped ? "/spools?view=materials" : "/spools", replace: true);
    }

    private IEnumerable<SpoolMaterialGroup> MaterialGroups => _spools
        .GroupBy(s => s.FilamentMaterialId)
        .Select(g => new SpoolMaterialGroup(
            g.First().MaterialBrand, g.First().MaterialType, g.First().MaterialColorHex, g.First().MaterialColorName,
            g.OrderBy(s => s.RemainingWeightGrams).ToList(),
            g.Sum(s => s.RemainingWeightGrams),
            g.Sum(s => (decimal)(s.MaterialWeightGrams ?? 0))))
        .OrderBy(g => g.Brand).ThenBy(g => g.Type).ThenBy(g => g.ColorName);

    private string LocationLabel(SpoolDto s) => s.PrinterName ?? s.StorageLocationName ?? s.DryerName ?? string.Empty;

    private sealed record SpoolMaterialGroup(string Brand, string Type, string ColorHex, string? ColorName,
        List<SpoolDto> Spools, decimal TotalGrams, decimal CapacityGrams);

    private void ConfirmDelete(SpoolDto spool) => _deleteTarget = spool;

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        await Spools.DeleteAsync(_deleteTarget.Id);
        _deleteTarget = null;
        await LoadAsync();
    }

    private async Task CopyAsync(SpoolDto spool)
    {
        var resp = await Spools.CreateAsync(new CreateSpoolRequest
        {
            FilamentMaterialId = spool.FilamentMaterialId,
            RemainingWeightGrams = spool.RemainingWeightGrams,
            RemainingPercent = spool.RemainingPercent,
            PrinterId = spool.PrinterId,
            StorageLocationId = spool.StorageLocationId,
            PurchasedAt = spool.PurchasedAt,
            PurchasePrice = spool.PurchasePrice,
            ReorderUrl = spool.ReorderUrl,
            Notes = spool.Notes,
            OwnerName = spool.OwnerName,
            EmptySpoolWeightGrams = spool.EmptySpoolWeightGrams
        });
        if (resp.IsSuccessStatusCode)
            await LoadAsync();
    }

    private async Task WriteNfcAsync(SpoolDto spool)
    {
        _nfcTarget = spool;
        _nfcWriting = true;
        _nfcMessage = null;
        _nfcSuccess = false;
        StateHasChanged();

        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = spool.Id });
        if (encoded?.JsonPayload == null)
        {
            _nfcMessage = L["common.error"];
            _nfcSuccess = false;
            _nfcWriting = false;
            StateHasChanged();
            return;
        }

        await Nfc.WriteAsync(encoded.JsonPayload, DotNetObjectReference.Create(this), encoded.SpoolmanId);
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

    private async Task ShowSpoolJsonAsync(Guid spoolId)
    {
        if (_jsonTarget == spoolId)
        {
            _jsonTarget = null;
            _jsonPayload = null;
            return;
        }
        _jsonCopied = false;
        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = spoolId });
        _jsonTarget = spoolId;
        _jsonPayload = encoded?.JsonPayload;
    }

    private async Task CopyJsonAsync()
    {
        if (_jsonPayload == null)
            return;
        await JS.InvokeVoidAsync("clipboardHelper.copy", _jsonPayload);
        _jsonCopied = true;
    }

    public void Dispose() { _searchTimer?.Dispose(); _clockTimer?.Dispose(); }
}

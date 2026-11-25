using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Components;
using SpoolManager.Client.Helpers;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;
using SpoolManager.Shared.Helpers;
using Timer = System.Timers.Timer;

namespace SpoolManager.Client.Pages.Spools;

public partial class SpoolDetail : IDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private Guid _loadedId;
    private bool _loading = true;
    private SpoolDto? _spool;
    private FilamentMaterialDto? _material;
    private List<PrinterDto> _printers = [];
    private List<StorageLocationDto> _storages = [];
    private List<DryerDto> _dryers = [];
    private Timer? _clockTimer;
    private string? _error;

    // weigh
    private ElementReference _grossInput;
    private decimal? _grossWeight;
    private decimal? _tareWeight;
    private bool _editTare;
    private bool _weighing;
    private string? _weighError;
    private string? _weighDone;

    // manual remaining
    private bool _showRemainingForm;
    private decimal _remainingGrams;
    private decimal _remainingPct;

    // location
    private string? _movingTo;
    private string? _moveMessage;
    private bool _moveFailed;
    private int _moveVersion;

    // lifecycle
    private bool _confirmConsume;
    private bool _busy;

    // delete
    private bool _confirmDelete;
    private bool _deleting;

    // tags
    private bool _nfcSupported;
    private bool _writingNfc;
    private string? _nfcMessage;
    private bool _nfcSuccess;
    private string? _tagJson;
    private bool _tagCopied;
    private FindSpoolModal? _findModal;

    [JSInvokable] public void OnWriteSuccess() { _nfcMessage = L["tag.write.success"]; _nfcSuccess = true; _writingNfc = false; StateHasChanged(); }
    [JSInvokable] public void OnWriteError(string message) { _nfcMessage = message; _nfcSuccess = false; _writingNfc = false; StateHasChanged(); }
    [JSInvokable] public void OnScanStarted() { }
    [JSInvokable] public void OnTagRead(string json, string serialNumber) { }
    [JSInvokable] public void OnReadError(string message) { }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _nfcSupported = await Nfc.CheckSupportAsync();
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Project.CurrentProject == null)
        {
            _loading = false;
            return;
        }
        if (_loadedId == Id)
            return;
        _loadedId = Id;
        _loading = true;
        _spool = null;
        _material = null;
        _tagJson = null;
        _nfcMessage = null;
        _confirmDelete = _confirmConsume = false;
        _moveMessage = null;
        _weighDone = null;

        try
        {
            var spoolTask = Spools.GetByIdAsync(Id);
            var pt = Printers.GetAllAsync();
            var st = Storage.GetAllAsync();
            var dt = Dryers.GetAllAsync();
            await Task.WhenAll(pt, st, dt);
            _printers = await pt ?? [];
            _storages = await st ?? [];
            _dryers = await dt ?? [];
            _spool = await spoolTask;
        }
        catch (Exception)
        {
            _spool = null;
        }

        if (_spool != null)
        {
            ResetWeighForm();
            try { _material = await Materials.GetByIdAsync(_spool.FilamentMaterialId); }
            catch (Exception) { _material = null; }
        }
        _loading = false;
    }

    private async Task RefreshAsync()
    {
        try { _spool = await Spools.GetByIdAsync(Id) ?? _spool; }
        catch (Exception) { _error = L["common.error"]; }
    }

    // ── Weigh ─────────────────────────────────────────────────────────────
    private decimal? DefaultTare => SpoolWeightCalculator.EffectiveTare(_spool?.EmptySpoolWeightGrams, _spool?.MaterialSpoolWeightGrams);

    private decimal? WeighResult => _grossWeight is { } g && _tareWeight is { } t ? SpoolWeightCalculator.RemainingFromGross(g, t) : null;

    private void ResetWeighForm()
    {
        _grossWeight = null;
        _tareWeight = DefaultTare;
        _editTare = DefaultTare == null;
        _weighError = null;
        _remainingGrams = _spool?.RemainingWeightGrams ?? 0;
        _remainingPct = _spool?.RemainingPercent ?? 0;
    }

    private async Task FocusWeighAsync()
    {
        // Focusing also scrolls the weigh card into view
        try { await _grossInput.FocusAsync(); } catch (Exception) { }
    }

    private async Task WeighAsync()
    {
        if (_grossWeight == null || _weighing)
            return;
        if (_tareWeight is not > 0)
        {
            _weighError = L["spool.weigh.tare.missing"];
            return;
        }
        _weighing = true;
        _weighError = null;
        _weighDone = null;
        try
        {
            // Only store a spool-specific empty weight when the user changed it
            var tare = _tareWeight != DefaultTare || _spool?.EmptySpoolWeightGrams is > 0 ? _tareWeight : null;
            var resp = await Spools.WeighAsync(Id, new WeighSpoolRequest { GrossWeightGrams = _grossWeight.Value, EmptySpoolWeightGrams = tare });
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _weighError = body.Contains("empty_spool_weight_unknown") ? L["spool.weigh.tare.missing"] : L["common.error"];
                return;
            }
            await RefreshAsync();
            _weighDone = L.Format("spool.weigh.saved", SpoolUi.Grams(_spool!.RemainingWeightGrams));
            ResetWeighForm();
        }
        finally
        {
            _weighing = false;
        }
    }

    private void OpenRemainingForm()
    {
        _remainingGrams = _spool?.RemainingWeightGrams ?? 0;
        _remainingPct = _spool?.RemainingPercent ?? 0;
        _showRemainingForm = true;
    }

    private void SetRemainingGrams(decimal value)
    {
        _remainingGrams = value;
        if (_spool?.MaterialWeightGrams is > 0)
            _remainingPct = Math.Round(Math.Min(100, value / _spool.MaterialWeightGrams.Value * 100m), 1);
    }

    private void SetRemainingPct(decimal value)
    {
        _remainingPct = value;
        if (_spool?.MaterialWeightGrams is > 0)
            _remainingGrams = Math.Round(value / 100m * _spool.MaterialWeightGrams.Value, 1);
    }

    private async Task UpdateRemainingAsync()
    {
        _weighError = null;
        if (_remainingGrams < 0 || _remainingPct is < 0 or > 100)
        {
            _weighError = L["spool.remaining.invalid"];
            return;
        }
        var resp = await Spools.UpdateRemainingAsync(Id, new UpdateRemainingRequest { RemainingWeightGrams = _remainingGrams, RemainingPercent = _remainingPct });
        if (!resp.IsSuccessStatusCode)
        {
            _weighError = L["common.error"];
            return;
        }
        await RefreshAsync();
        _showRemainingForm = false;
        _weighDone = L.Format("spool.weigh.saved", SpoolUi.Grams(_spool!.RemainingWeightGrams));
    }

    // ── Location ──────────────────────────────────────────────────────────
    private string CurrentLocationKey => _spool == null ? "" :
        _spool.PrinterId != null ? "p:" + _spool.PrinterId :
        _spool.StorageLocationId != null ? "s:" + _spool.StorageLocationId :
        _spool.DryerId != null ? "d:" + _spool.DryerId : "none";

    private async Task MoveAsync(string key, string name)
    {
        if (_spool == null || _movingTo != null || key == CurrentLocationKey)
            return;
        _movingTo = key;
        _moveMessage = null;
        _moveFailed = false;
        Guid? id = key.Length > 2 && Guid.TryParse(key[2..], out var g) ? g : null;
        try
        {
            var resp = await Spools.UpdateAsync(Id, new UpdateSpoolRequest
            {
                FilamentMaterialId = _spool.FilamentMaterialId,
                RemainingWeightGrams = _spool.RemainingWeightGrams,
                RemainingPercent = _spool.RemainingPercent,
                RfidTagUid = _spool.RfidTagUid,
                PrinterId = key.StartsWith("p:") ? id : null,
                StorageLocationId = key.StartsWith("s:") ? id : null,
                DryerId = key.StartsWith("d:") ? id : null,
                PurchasedAt = _spool.PurchasedAt,
                PurchasePrice = _spool.PurchasePrice,
                ReorderUrl = _spool.ReorderUrl,
                Notes = _spool.Notes,
                OwnerName = _spool.OwnerName,
                EmptySpoolWeightGrams = _spool.EmptySpoolWeightGrams
            });
            if (resp.IsSuccessStatusCode)
            {
                await RefreshAsync();
                _moveMessage = key == "none" ? L["spool.location.removed"] : L.Format("spool.location.moved", name);
            }
            else
            {
                _moveFailed = true;
                _moveMessage = L["common.error"];
            }
        }
        catch (Exception)
        {
            _moveFailed = true;
            _moveMessage = L["common.error"];
        }
        finally
        {
            _movingTo = null;
            // re-create the radio inputs so the checked state always mirrors the saved location
            _moveVersion++;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private async Task RunAsync(Func<Guid, Task<HttpResponseMessage>> action)
    {
        if (_busy)
            return;
        _busy = true;
        _error = null;
        try
        {
            var resp = await action(Id);
            if (!resp.IsSuccessStatusCode)
                _error = L["common.error"];
            await RefreshAsync();
        }
        catch (Exception)
        {
            _error = L["common.error"];
        }
        finally
        {
            _busy = false;
        }
    }

    private Task MarkOpenedAsync() => RunAsync(Spools.MarkOpenedAsync);
    private Task MarkDriedAsync() => RunAsync(Spools.MarkDriedAsync);
    private Task MarkRepackagedAsync() => RunAsync(Spools.MarkRepackagedAsync);
    private Task MarkReopenedAsync() => RunAsync(Spools.MarkReopenedAsync);

    private async Task MarkConsumedAsync()
    {
        _confirmConsume = false;
        await RunAsync(Spools.MarkConsumedAsync);
        ResetWeighForm();
    }

    private sealed record TimelineEntry(DateTime Sort, string Title, string Sub, string Kind);

    private List<TimelineEntry> Timeline
    {
        get
        {
            if (_spool == null)
                return [];
            var s = _spool;
            var list = new List<TimelineEntry>();
            if (s.ConsumedAt is { } c)
                list.Add(new(SpoolUi.AsUtc(c), L["spool.timeline.consumed"], SpoolUi.LocalDate(c), "done"));
            if (s.ReopenedAt is { } ro)
                list.Add(new(SpoolUi.AsUtc(ro), L["spool.timeline.reopened"], SpoolUi.LocalDate(ro), "info"));
            if (s.RepackagedAt is { } rp)
                list.Add(new(SpoolUi.AsUtc(rp), L["spool.timeline.repackaged"], SpoolUi.LocalDate(rp), "muted"));
            if (s.DriedAt is { } d)
                list.Add(new(SpoolUi.AsUtc(d), L["spool.timeline.dried"], SpoolUi.LocalDateTime(d) + (s.MaterialDryTimeHours is > 0 ? $" · {s.MaterialDryTimeHours} h" : ""), "warn"));
            if (s.OpenedAt is { } o)
                list.Add(new(SpoolUi.AsUtc(o), L["spool.timeline.opened"], SpoolUi.LocalDate(o), "accent"));
            list.Add(new(SpoolUi.AsUtc(s.CreatedAt), L["spool.timeline.added"], SpoolUi.LocalDate(s.CreatedAt), "muted"));
            if (s.PurchasedAt is { } p)
                list.Add(new(DateTime.SpecifyKind(p, DateTimeKind.Utc), L["spool.timeline.purchased"],
                    SpoolUi.PlainDate(p) + (s.PurchasePrice is { } price ? " · " + price.ToString("C") : ""), "muted"));
            return [.. list.OrderByDescending(e => e.Sort)];
        }
    }

    // ── Tags ──────────────────────────────────────────────────────────────
    private async Task FindSpoolAsync()
    {
        if (_findModal != null)
            await _findModal.OpenAsync();
    }

    private async Task WriteNfcAsync()
    {
        _writingNfc = true;
        _nfcMessage = null;
        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = Id });
        if (encoded?.JsonPayload == null)
        {
            _nfcMessage = L["common.error"];
            _nfcSuccess = false;
            _writingNfc = false;
            return;
        }
        await Nfc.WriteAsync(encoded.JsonPayload, DotNetObjectReference.Create(this), encoded.SpoolmanId);
    }

    private async Task ToggleTagJsonAsync()
    {
        if (_tagJson != null)
        {
            _tagJson = null;
            return;
        }
        _tagCopied = false;
        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = Id });
        _tagJson = encoded?.JsonPayload ?? L["common.error"];
    }

    private async Task CopyTagJsonAsync()
    {
        if (_tagJson == null)
            return;
        await JS.InvokeVoidAsync("clipboardHelper.copy", _tagJson);
        _tagCopied = true;
    }

    // ── Delete ────────────────────────────────────────────────────────────
    private async Task DeleteAsync()
    {
        if (_deleting)
            return;
        _deleting = true;
        try
        {
            var resp = await Spools.DeleteAsync(Id);
            if (resp.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/spools");
                return;
            }
            _error = L["common.error"];
            _confirmDelete = false;
        }
        finally
        {
            _deleting = false;
        }
    }

    private string? ReorderUrl => !string.IsNullOrWhiteSpace(_spool?.ReorderUrl) ? _spool!.ReorderUrl : _material?.ReorderUrl;

    private void TrackReorder()
    {
        if (string.IsNullOrWhiteSpace(_spool?.ReorderUrl) && _material != null)
            _ = Tickets.TrackReorderClickAsync(_material.Id);
    }

    public void Dispose() => _clockTimer?.Dispose();
}

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Components;
using SpoolManager.Client.Helpers;
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
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // URL state – the global search in the top bar navigates to /spools?search=…
    [SupplyParameterFromQuery(Name = "search")] public string? QSearch { get; set; }
    [SupplyParameterFromQuery(Name = "view")] public string? QView { get; set; }
    [SupplyParameterFromQuery(Name = "status")] public string? QStatus { get; set; }
    [SupplyParameterFromQuery(Name = "type")] public string? QType { get; set; }
    [SupplyParameterFromQuery(Name = "owner")] public string? QOwner { get; set; }
    [SupplyParameterFromQuery(Name = "location")] public string? QLocation { get; set; }
    [SupplyParameterFromQuery(Name = "low")] public string? QLow { get; set; }
    [SupplyParameterFromQuery(Name = "sort")] public string? QSort { get; set; }

    private bool _initialized;
    private bool _loading = true;
    private bool _loadFailed;
    private List<SpoolDto> _spools = [];
    private List<string> _owners = [];

    private string _search = string.Empty;
    private string? _urlSearch;
    private string _status = "active";
    private string _type = string.Empty;
    private string _owner = string.Empty;
    private string _location = string.Empty;
    private bool _lowOnly;
    private string _sort = "low";
    private bool _grouped;

    // server-side filter values of the last load (search runs client-side so it also finds colours, places and ids)
    private (string status, string owner)? _loadedWith;

    private Timer? _searchTimer;
    private Timer? _clockTimer;

    private SpoolDto? _deleteTarget;
    private bool _deleting;
    private string? _actionError;

    private bool _nfcAvailable;
    private SpoolDto? _nfcTarget;
    private bool _nfcWriting;
    private bool _nfcSuccess;
    private string? _nfcMessage;

    private SpoolDto? _jsonTarget;
    private string? _jsonPayload;
    private bool _jsonLoading;
    private bool _jsonCopied;

    private SpoolDto? _findTarget;
    private FindSpoolModal? _findModal;

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
        {
            _loading = false;
            return;
        }
        try { _owners = await Spools.GetOwnersAsync() ?? []; } catch (Exception) { _owners = []; }
        _nfcAvailable = await Nfc.CheckSupportAsync();
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
        _initialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Project.CurrentProject == null)
            return;

        // Take over the search only if it was changed from outside (top bar, links)
        var qs = QSearch ?? string.Empty;
        if (_urlSearch == null || qs != _urlSearch)
            _search = qs;
        _urlSearch = qs;

        _grouped = string.Equals(QView, "materials", StringComparison.OrdinalIgnoreCase);
        _status = QStatus is "consumed" or "all" ? QStatus : "active";
        _type = QType ?? string.Empty;
        _owner = QOwner ?? string.Empty;
        _location = QLocation ?? string.Empty;
        _lowOnly = QLow == "1";
        _sort = QSort is "new" or "name" ? QSort : "low";

        if (_initialized && _loadedWith != (_status, _owner))
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loadFailed = false;
        var key = (_status, _owner);
        try
        {
            bool? consumed = _status == "active" ? false : _status == "consumed" ? true : null;
            _spools = await Spools.GetAllAsync(consumed: consumed, owner: string.IsNullOrEmpty(_owner) ? null : _owner) ?? [];
            _loadedWith = key;
        }
        catch (Exception)
        {
            _spools = [];
            _loadFailed = true;
        }
        _loading = false;
    }

    private async Task ReloadAsync()
    {
        _loadedWith = null;
        await LoadAsync();
    }

    // ── URL handling ──────────────────────────────────────────────────────
    private string BuildUrl()
    {
        var q = new List<string>();
        void Add(string k, string? v)
        {
            if (!string.IsNullOrEmpty(v))
                q.Add($"{k}={Uri.EscapeDataString(v)}");
        }
        Add("search", _search.Trim());
        Add("view", _grouped ? "materials" : null);
        Add("status", _status == "active" ? null : _status);
        Add("type", _type);
        Add("owner", _owner);
        Add("location", _location);
        Add("low", _lowOnly ? "1" : null);
        Add("sort", _sort == "low" ? null : _sort);
        return q.Count == 0 ? "/spools" : "/spools?" + string.Join("&", q);
    }

    private void ApplyUrl()
    {
        _urlSearch = _search.Trim();
        Nav.NavigateTo(BuildUrl(), replace: true);
    }

    private void OnSearch(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        _searchTimer?.Stop();
        _searchTimer?.Dispose();
        _searchTimer = new Timer(350) { AutoReset = false };
        _searchTimer.Elapsed += (_, _) => InvokeAsync(ApplyUrl);
        _searchTimer.Start();
    }

    private void ClearSearch()
    {
        _search = string.Empty;
        ApplyUrl();
    }

    private void SetStatus(string status) { _status = status; ApplyUrl(); }
    private void SetType(string type) { _type = _type == type ? string.Empty : type; ApplyUrl(); }
    private void ToggleLow() { _lowOnly = !_lowOnly; ApplyUrl(); }
    private void SetGrouped(bool grouped) { _grouped = grouped; ApplyUrl(); }

    private void OnOwnerChanged(ChangeEventArgs e) { _owner = e.Value?.ToString() ?? string.Empty; ApplyUrl(); }
    private void OnLocationChanged(ChangeEventArgs e) { _location = e.Value?.ToString() ?? string.Empty; ApplyUrl(); }
    private void OnSortChanged(ChangeEventArgs e) { _sort = e.Value?.ToString() ?? "low"; ApplyUrl(); }

    private void ResetFilters()
    {
        _search = string.Empty;
        _status = "active";
        _type = _owner = _location = string.Empty;
        _lowOnly = false;
        ApplyUrl();
    }

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(_search) || _status != "active" || _type != "" || _owner != "" || _location != "" || _lowOnly;

    // ── Derived data ──────────────────────────────────────────────────────
    private List<string> Types => [.. _spools.Select(s => s.MaterialType.Trim()).Where(t => t != "")
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];

    private sealed record LocationOption(string Key, string Kind, string Name);

    private List<LocationOption> Locations
    {
        get
        {
            var list = new List<LocationOption>();
            list.AddRange(_spools.Where(s => s.PrinterId != null).Select(s => new LocationOption("p:" + s.PrinterId, "printer", s.PrinterName ?? "?")));
            list.AddRange(_spools.Where(s => s.StorageLocationId != null).Select(s => new LocationOption("s:" + s.StorageLocationId, "storage", s.StorageLocationName ?? "?")));
            list.AddRange(_spools.Where(s => s.DryerId != null).Select(s => new LocationOption("d:" + s.DryerId, "dryer", s.DryerName ?? "?")));
            return [.. list.DistinctBy(l => l.Key).OrderBy(l => l.Kind).ThenBy(l => l.Name)];
        }
    }

    private bool MatchesLocation(SpoolDto s) => _location switch
    {
        "" => true,
        "none" => !SpoolUi.HasLocation(s),
        _ when _location.StartsWith("p:") => s.PrinterId?.ToString() == _location[2..],
        _ when _location.StartsWith("s:") => s.StorageLocationId?.ToString() == _location[2..],
        _ when _location.StartsWith("d:") => s.DryerId?.ToString() == _location[2..],
        _ => true
    };

    private static bool MatchesSearch(SpoolDto s, string[] terms)
    {
        if (terms.Length == 0)
            return true;
        var text = string.Join(' ', s.MaterialBrand, s.MaterialType, s.MaterialColorName, "#" + s.MaterialColorHex, s.OwnerName,
            s.PrinterName, s.StorageLocationName, s.DryerName, "#" + SpoolUi.ShortId(s), s.RfidTagUid, s.Notes,
            s.SpoolmanId > 0 ? s.SpoolmanId.ToString() : null);
        return terms.All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private string[] SearchTerms => _search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private IEnumerable<SpoolDto> FilteredUnsorted => _spools
        .Where(s => MatchesSearch(s, SearchTerms))
        .Where(s => _type == "" || string.Equals(s.MaterialType.Trim(), _type, StringComparison.OrdinalIgnoreCase))
        .Where(s => !_lowOnly || SpoolUi.IsLow(s))
        .Where(MatchesLocation);

    private List<SpoolDto> Filtered => _sort switch
    {
        "new" => [.. FilteredUnsorted.OrderByDescending(s => s.CreatedAt)],
        "name" => [.. FilteredUnsorted.OrderBy(s => s.MaterialType).ThenBy(s => s.MaterialColorName).ThenBy(s => s.MaterialBrand)],
        _ => [.. FilteredUnsorted.OrderBy(s => s.ConsumedAt != null).ThenBy(s => s.RemainingPercent)]
    };

    private int LowCount => _spools.Count(SpoolUi.IsLow);

    private sealed record MaterialGroup(Guid MaterialId, string Brand, string Type, string ColorHex, string? ColorName,
        List<SpoolDto> Spools, decimal TotalGrams, decimal CapacityGrams)
    {
        public decimal Percent => CapacityGrams > 0 ? Math.Min(TotalGrams / CapacityGrams * 100, 100) : 0;
    }

    private sealed record TypeSection(string Type, List<MaterialGroup> Materials)
    {
        public int SpoolCount => Materials.Sum(m => m.Spools.Count);
        public decimal TotalGrams => Materials.Sum(m => m.TotalGrams);
    }

    private List<TypeSection> Sections(List<SpoolDto> spools) => [.. spools
        .GroupBy(s => s.FilamentMaterialId)
        .Select(g => new MaterialGroup(g.Key,
            g.First().MaterialBrand, g.First().MaterialType, g.First().MaterialColorHex, g.First().MaterialColorName,
            [.. g.OrderBy(s => s.RemainingPercent)],
            g.Sum(s => s.RemainingWeightGrams),
            g.Sum(s => (decimal)(s.MaterialWeightGrams ?? 0))))
        .GroupBy(m => m.Type.Trim().ToUpperInvariant())
        .Select(g => new TypeSection(g.First().Type, [.. g.OrderBy(m => m.Brand).ThenBy(m => m.ColorName)]))
        .OrderByDescending(s => s.SpoolCount).ThenBy(s => s.Type)];

    // ── Actions ───────────────────────────────────────────────────────────
    private async Task FindSpoolAsync(SpoolDto spool)
    {
        _findTarget = spool;
        StateHasChanged();
        await Task.Yield();
        if (_findModal != null)
            await _findModal.OpenAsync();
    }

    private void ConfirmDelete(SpoolDto spool)
    {
        _actionError = null;
        _deleteTarget = spool;
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null || _deleting)
            return;
        _deleting = true;
        try
        {
            var resp = await Spools.DeleteAsync(_deleteTarget.Id);
            if (!resp.IsSuccessStatusCode)
            {
                _actionError = L["common.error"];
                return;
            }
            _deleteTarget = null;
            await ReloadAsync();
        }
        finally
        {
            _deleting = false;
        }
    }

    private async Task CopyAsync(SpoolDto spool)
    {
        _actionError = null;
        var resp = await Spools.CreateAsync(new CreateSpoolRequest
        {
            FilamentMaterialId = spool.FilamentMaterialId,
            RemainingWeightGrams = spool.RemainingWeightGrams,
            RemainingPercent = spool.RemainingPercent,
            PrinterId = spool.PrinterId,
            StorageLocationId = spool.StorageLocationId,
            DryerId = spool.DryerId,
            PurchasedAt = spool.PurchasedAt,
            PurchasePrice = spool.PurchasePrice,
            ReorderUrl = spool.ReorderUrl,
            Notes = spool.Notes,
            OwnerName = spool.OwnerName,
            EmptySpoolWeightGrams = spool.EmptySpoolWeightGrams
        });
        if (resp.IsSuccessStatusCode)
            await ReloadAsync();
        else
            _actionError = L["common.error"];
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

    private void DismissNfc()
    {
        _nfcTarget = null;
        _nfcMessage = null;
    }

    private async Task ShowSpoolJsonAsync(SpoolDto spool)
    {
        _jsonCopied = false;
        _jsonTarget = spool;
        _jsonPayload = null;
        _jsonLoading = true;
        StateHasChanged();
        var encoded = await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = spool.Id });
        _jsonPayload = encoded?.JsonPayload ?? L["common.error"];
        _jsonLoading = false;
    }

    private void CloseJson()
    {
        _jsonTarget = null;
        _jsonPayload = null;
    }

    private async Task CopyJsonAsync()
    {
        if (_jsonPayload == null)
            return;
        await JS.InvokeVoidAsync("clipboardHelper.copy", _jsonPayload);
        _jsonCopied = true;
    }

    private string GroupMeta(int count, decimal grams) => count == 1
        ? L.Format("dashboard.shelf.group.meta.one", SpoolUi.Weight(grams))
        : L.Format("dashboard.shelf.group.meta", count, SpoolUi.Weight(grams));

    private string SummaryText(List<SpoolDto> list)
    {
        var grams = list.Sum(s => s.RemainingWeightGrams);
        return list.Count == 1
            ? L.Format("spool.list.summary.one", SpoolUi.Weight(grams))
            : L.Format("spool.list.summary", list.Count, SpoolUi.Weight(grams));
    }

    public void Dispose()
    {
        _searchTimer?.Dispose();
        _clockTimer?.Dispose();
    }
}

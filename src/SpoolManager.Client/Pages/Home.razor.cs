using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Helpers;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using Timer = System.Timers.Timer;

namespace SpoolManager.Client.Pages;

public partial class Home : IDisposable
{
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private const int MaxAttentionCards = 6;

    private bool _loading = true;
    private bool _loadFailed;
    private bool _showOnboarding;
    private bool _onboardingSaving;
    private int _printersCount;
    private int _storageCount;
    private int _total;
    private int _storedCount;
    private Timer? _clockTimer;

    private List<SpoolDto> _active = [];
    private List<SpoolDto> _recent = [];
    private List<AttentionItem> _attention = [];
    private int _attentionTotal;
    private List<ShelfGroup> _shelf = [];
    private List<ActivePlace> _places = [];
    private decimal _totalGrams;
    private int _lowCount;
    private int _inPrinterCount;
    private int _consumedCount;

    private enum AttentionKind { Low, DryingDone, NoLocation }

    private sealed record AttentionItem(AttentionKind Kind, SpoolDto? Spool, int Count, string? ReorderUrl, Guid? ReorderMaterialId);

    private sealed record ShelfGroup(string Type, List<SpoolDto> Spools, decimal TotalGrams, decimal CapacityGrams, int LowCount);

    private sealed record ActivePlace(string Name, bool IsDryer, Guid Id, List<SpoolDto> Spools);

    protected override void OnInitialized()
    {
        Project.OnProjectChanged += OnProjectChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login", replace: true);
            return;
        }
        _showOnboarding = Auth.CurrentUser?.DashboardOnboardingDismissed != true;
        await LoadDataAsync();
        // Keep drying progress current
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
    }

    private void OnProjectChanged() => _ = InvokeAsync(LoadDataAsync);

    private async Task LoadDataAsync()
    {
        if (Project.CurrentProject == null)
        {
            _loading = false;
            StateHasChanged();
            return;
        }
        _loading = true;
        _loadFailed = false;
        StateHasChanged();

        List<SpoolDto> all;
        List<PrinterDto> printers;
        List<DryerDto> dryers;
        try
        {
            var pt = Printers.GetAllAsync();
            var st = Storage.GetAllAsync();
            var dt = Dryers.GetAllAsync();
            var spt = Spools.GetAllAsync();
            await Task.WhenAll(pt, st, dt, spt);
            printers = await pt ?? [];
            _storageCount = (await st ?? []).Count;
            dryers = await dt ?? [];
            all = await spt ?? [];
        }
        catch (Exception)
        {
            _loadFailed = true;
            _loading = false;
            StateHasChanged();
            return;
        }

        _printersCount = printers.Count;
        _total = all.Count;
        _active = [.. all.Where(SpoolUi.IsActive)];
        _consumedCount = all.Count - _active.Count;
        _storedCount = _active.Count(s => s.StorageLocationId != null);
        _totalGrams = _active.Sum(s => s.RemainingWeightGrams);
        _lowCount = _active.Count(SpoolUi.IsLow);
        _inPrinterCount = _active.Count(s => s.PrinterId != null);
        _recent = [.. all.OrderByDescending(s => s.CreatedAt).Take(6)];

        _shelf = [.. _active
            .GroupBy(s => s.MaterialType.Trim().ToUpperInvariant())
            .Select(g => new ShelfGroup(
                g.First().MaterialType,
                [.. g.OrderBy(s => s.RemainingPercent)],
                g.Sum(s => s.RemainingWeightGrams),
                g.Sum(s => (decimal)(s.MaterialWeightGrams ?? 0)),
                g.Count(SpoolUi.IsLow)))
            .OrderByDescending(g => g.Spools.Count).ThenBy(g => g.Type)];

        var places = new List<ActivePlace>();
        foreach (var p in printers)
        {
            var spools = _active.Where(s => s.PrinterId == p.Id).ToList();
            if (spools.Count > 0)
                places.Add(new ActivePlace(p.Name, false, p.Id, spools));
        }
        foreach (var d in dryers)
        {
            var spools = _active.Where(s => s.DryerId == d.Id).ToList();
            if (spools.Count > 0)
                places.Add(new ActivePlace(d.Name, true, d.Id, spools));
        }
        _places = places;

        await BuildAttentionAsync();

        if (_showOnboarding && !_onboardingSaving && IsOnboardingCompleted())
        {
            _onboardingSaving = true;
            var saved = await Auth.SetDashboardOnboardingDismissedAsync(true);
            _onboardingSaving = false;
            if (saved)
                _showOnboarding = false;
        }

        _loading = false;
        StateHasChanged();
    }

    private async Task BuildAttentionAsync()
    {
        var items = new List<AttentionItem>();

        foreach (var s in _active.Where(s => SpoolUi.GetDrying(s)?.Done == true))
            items.Add(new AttentionItem(AttentionKind.DryingDone, s, 1, null, null));

        var low = _active.Where(SpoolUi.IsLow).OrderBy(s => s.RemainingPercent).ToList();
        Dictionary<Guid, string> materialReorder = [];
        var missing = low.Where(s => string.IsNullOrWhiteSpace(s.ReorderUrl)).Select(s => s.FilamentMaterialId).Distinct().ToList();
        if (missing.Count > 0)
        {
            try
            {
                var mats = await Http.GetFromJsonAsync<List<FilamentMaterialDto>>(
                    $"api/materials?ids={Uri.EscapeDataString(string.Join(",", missing))}") ?? [];
                materialReorder = mats.Where(m => !string.IsNullOrWhiteSpace(m.ReorderUrl)).ToDictionary(m => m.Id, m => m.ReorderUrl!);
            }
            catch (Exception)
            {
                // Reorder links are optional – the card falls back to "open spool".
            }
        }
        foreach (var s in low)
        {
            if (!string.IsNullOrWhiteSpace(s.ReorderUrl))
                items.Add(new AttentionItem(AttentionKind.Low, s, 1, s.ReorderUrl, null));
            else if (materialReorder.TryGetValue(s.FilamentMaterialId, out var url))
                items.Add(new AttentionItem(AttentionKind.Low, s, 1, url, s.FilamentMaterialId));
            else
                items.Add(new AttentionItem(AttentionKind.Low, s, 1, null, null));
        }

        var homeless = _active.Where(s => !SpoolUi.HasLocation(s)).ToList();
        if (homeless.Count > 0)
            items.Add(new AttentionItem(AttentionKind.NoLocation, homeless.Count == 1 ? homeless[0] : null, homeless.Count, null, null));

        _attentionTotal = items.Count;
        _attention = [.. items.Take(MaxAttentionCards)];
    }

    private void TrackReorder(AttentionItem item)
    {
        if (item.ReorderMaterialId is { } id)
            _ = Tickets.TrackReorderClickAsync(id);
    }

    private string Greeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var key = hour < 5 ? "dashboard.greeting.evening" : hour < 12 ? "dashboard.greeting.morning" : hour < 18 ? "dashboard.greeting.afternoon" : "dashboard.greeting.evening";
            var name = Auth.CurrentUser?.Username;
            return string.IsNullOrWhiteSpace(name) ? L[key + ".anon"] : L.Format(key, name);
        }
    }

    private string AttentionSummary => _attentionTotal switch
    {
        0 => L["dashboard.attention.none"],
        1 => L["dashboard.attention.one"],
        _ => L.Format("dashboard.attention.many", _attentionTotal)
    };

    public void Dispose()
    {
        Project.OnProjectChanged -= OnProjectChanged;
        _clockTimer?.Dispose();
    }

    private async Task SkipOnboardingAsync()
    {
        if (_onboardingSaving)
            return;
        _onboardingSaving = true;
        var saved = await Auth.SetDashboardOnboardingDismissedAsync(true);
        _onboardingSaving = false;
        if (saved)
            _showOnboarding = false;
    }

    private bool IsOnboardingCompleted() =>
        Project.CurrentProject != null
        && _printersCount > 0
        && _storageCount > 0
        && _total > 0
        && _storedCount > 0;

    private int OnboardingDoneCount =>
        (_printersCount > 0 ? 1 : 0) + (_storageCount > 0 ? 1 : 0) + (_total > 0 ? 1 : 0) + (_storedCount > 0 ? 1 : 0);

    private string GroupMeta(int count, decimal grams) => count == 1
        ? L.Format("dashboard.shelf.group.meta.one", SpoolUi.Weight(grams))
        : L.Format("dashboard.shelf.group.meta", count, SpoolUi.Weight(grams));

    private static int Fill(decimal value, decimal capacity) =>
        capacity <= 0 ? 0 : (int)Math.Clamp(Math.Round(value / capacity * 100), 0, 100);
}

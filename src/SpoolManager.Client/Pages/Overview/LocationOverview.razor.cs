using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Helpers;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using Timer = System.Timers.Timer;

namespace SpoolManager.Client.Pages.Overview;

public partial class LocationOverview : IDisposable
{
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private bool _loadFailed;
    private List<Place> _printers = [];
    private List<Place> _storages = [];
    private List<Place> _dryers = [];
    private List<SpoolDto> _unplaced = [];
    private int _activeCount;
    private Timer? _clockTimer;

    private sealed record Place(Guid Id, string Name, string? Description, List<SpoolDto> Spools)
    {
        public decimal TotalGrams => Spools.Sum(s => s.RemainingWeightGrams);
    }

    private enum Kind { Printer, Storage, Dryer }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
        {
            _loading = false;
            return;
        }
        await LoadAsync();
        _clockTimer = new Timer(60_000);
        _clockTimer.Elapsed += (_, _) => InvokeAsync(StateHasChanged);
        _clockTimer.Start();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loadFailed = false;
        try
        {
            var pt = Printers.GetAllAsync();
            var st = Storage.GetAllAsync();
            var dt = Dryers.GetAllAsync();
            var spt = Spools.GetAllAsync(consumed: false);
            await Task.WhenAll(pt, st, dt, spt);
            var spools = await spt ?? [];
            _activeCount = spools.Count;
            _printers = [.. (await pt ?? []).Select(p => new Place(p.Id, p.Name, p.Notes, Sorted(spools.Where(s => s.PrinterId == p.Id))))];
            _storages = [.. (await st ?? []).Select(l => new Place(l.Id, l.Name, l.Description, Sorted(spools.Where(s => s.StorageLocationId == l.Id))))];
            _dryers = [.. (await dt ?? []).Select(d => new Place(d.Id, d.Name, d.Description, Sorted(spools.Where(s => s.DryerId == d.Id))))];
            _unplaced = Sorted(spools.Where(s => !SpoolUi.HasLocation(s)));
        }
        catch (Exception)
        {
            _loadFailed = true;
        }
        _loading = false;
    }

    private static List<SpoolDto> Sorted(IEnumerable<SpoolDto> spools) =>
        [.. spools.OrderBy(s => s.MaterialType).ThenBy(s => s.MaterialColorName)];

    private int PlacedCount => _activeCount - _unplaced.Count;

    private bool NothingSetUp => _printers.Count == 0 && _storages.Count == 0 && _dryers.Count == 0;

    private string CountText(int count) =>
        count == 0 ? L["overview.place.empty.count"] : count == 1 ? L["overview.place.one"] : L.Format("overview.place.many", count);

    public void Dispose() => _clockTimer?.Dispose();
}

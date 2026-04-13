using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;

namespace SpoolManager.Client.Pages.Overview;

public partial class LocationOverview
{
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private List<PrinterDto> _printers = [];
    private List<StorageLocationDto> _locations = [];
    private List<DryerDto> _dryers = [];
    private List<SpoolDto> _spools = [];

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        var pt = Printers.GetAllAsync();
        var st = Storage.GetAllAsync();
        var dt = Dryers.GetAllAsync();
        var spt = Spools.GetAllAsync();
        await Task.WhenAll(pt, st, dt, spt);
        _printers = await pt ?? [];
        _locations = await st ?? [];
        _dryers = await dt ?? [];
        _spools = await spt ?? [];
        _loading = false;
    }

}

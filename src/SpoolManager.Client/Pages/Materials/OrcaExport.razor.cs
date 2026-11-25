using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class OrcaExport
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _exporting;
    private bool _orcaSuccess;
    private string? _orcaError;
    private Guid? _pickerId;
    private List<FilamentMaterialDto> _selectedMaterials = [];

    private IReadOnlyCollection<Guid> SelectedIds => _selectedMaterials.Select(m => m.Id).ToHashSet();

    private List<FilamentMaterialDto> Warnings => _selectedMaterials.Where(NeedsTemps).ToList();

    private static bool NeedsTemps(FilamentMaterialDto m) => m.MinTempCelsius == 0 || m.MaxTempCelsius == 0;

    private async Task AddByIdAsync(Guid id)
    {
        if (_selectedMaterials.Any(m => m.Id == id))
            return;
        var material = await Materials.GetByIdAsync(id);
        if (material != null && _selectedMaterials.All(m => m.Id != id))
        {
            _selectedMaterials.Add(material);
            _orcaSuccess = false;
        }
    }

    private void OnPicked(FilamentMaterialDto? m)
    {
        if (m == null || _selectedMaterials.Any(x => x.Id == m.Id))
            return;
        _selectedMaterials.Add(m);
        _orcaSuccess = false;
        _pickerId = null;
    }

    private void Remove(Guid id) => _selectedMaterials.RemoveAll(m => m.Id == id);

    private void Clear() => _selectedMaterials.Clear();

    private Task ExportSelectedAsync() => DoExport(_selectedMaterials.Select(m => m.Id).ToList());

    private Task ExportAllAsync() => DoExport(null);

    private async Task DoExport(List<Guid>? ids)
    {
        _exporting = true;
        _orcaError = null;
        _orcaSuccess = false;
        try
        {
            var result = await Materials.ExportOrcaAsync(ids);
            if (result.HasValue)
            {
                await JS.InvokeVoidAsync("downloadHelper.saveAs", result.Value.Data, result.Value.Filename);
                _orcaSuccess = true;
            }
            else
            {
                _orcaError = L["material.orca.failed"];
            }
        }
        catch (Exception)
        {
            _orcaError = L["material.orca.failed"];
        }
        _exporting = false;
    }
}

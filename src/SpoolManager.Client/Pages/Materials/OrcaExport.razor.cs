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

    private void OnPicked(FilamentMaterialDto? m)
    {
        if (m == null)
            return;
        if (_selectedMaterials.Any(x => x.Id == m.Id))
            return;
        _selectedMaterials.Add(m);
        _pickerId = null;
    }

    private void Remove(Guid id) => _selectedMaterials.RemoveAll(m => m.Id == id);

    private async Task ExportSelectedAsync()
    {
        await DoExport(_selectedMaterials.Select(m => m.Id).ToList());
    }

    private async Task ExportAllAsync()
    {
        await DoExport(null);
    }

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
                _orcaError = "Export failed.";
            }
        }
        catch (Exception ex)
        {
            _orcaError = ex.Message;
        }
        _exporting = false;
    }
}

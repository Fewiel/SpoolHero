using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialShare
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _exporting;
    private bool _importing;
    private bool _copied;
    private Guid? _exportPickerId;
    private List<FilamentMaterialDto> _selectedMaterials = [];
    private string? _exportBase64;
    private string _importBase64 = string.Empty;
    private string? _importError;
    private string? _importSuccess;

    private void OnExportPicked(FilamentMaterialDto? m)
    {
        if (m == null || _selectedMaterials.Any(x => x.Id == m.Id))
            return;
        _selectedMaterials.Add(m);
        _exportPickerId = null;
    }

    private async Task ExportAsync()
    {
        _exporting = true;
        _exportBase64 = await Materials.ExportAsync(_selectedMaterials.Select(x => x.Id).ToList());
        _exporting = false;
    }

    private async Task CopyExportAsync()
    {
        if (_exportBase64 == null)
            return;
        _copied = await JS.InvokeAsync<bool>("clipboardHelper.copy", _exportBase64);
        if (_copied)
        {
            await Task.Delay(2000);
            _copied = false;
            StateHasChanged();
        }
    }

    private async Task DoImportAsync()
    {
        _importing = true;
        _importError = null;
        _importSuccess = null;

        var resp = await Materials.ImportAsync(_importBase64);
        _importing = false;

        if (resp.IsSuccessStatusCode)
        {
            var result = await resp.Content.ReadFromJsonAsync<ImportResult>();
            _importSuccess = L["material.import.success"].Replace("{0}", (result?.Imported ?? 0).ToString());
            _importBase64 = string.Empty;
        }
        else
        {
            _importError = await resp.Content.ReadAsStringAsync();
        }
    }

    private record ImportResult(int Imported);
}

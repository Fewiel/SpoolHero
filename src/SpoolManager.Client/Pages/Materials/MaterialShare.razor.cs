using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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

    private bool _importTab;
    private bool _exporting;
    private bool _importing;
    private bool _copied;
    private Guid? _exportPickerId;
    private List<FilamentMaterialDto> _selectedMaterials = [];
    private string? _exportBase64;
    private string? _exportError;
    private string _importBase64 = string.Empty;
    private string? _importError;
    private string? _importSuccess;

    private IReadOnlyCollection<Guid> SelectedIds => _selectedMaterials.Select(m => m.Id).ToHashSet();

    private void OnExportPicked(FilamentMaterialDto? m)
    {
        if (m == null || _selectedMaterials.Any(x => x.Id == m.Id))
            return;
        _selectedMaterials.Add(m);
        _exportBase64 = null;
        _exportPickerId = null;
    }

    private async Task AddByIdAsync(Guid id)
    {
        if (_selectedMaterials.Any(x => x.Id == id))
            return;
        var m = await Materials.GetByIdAsync(id);
        if (m != null && _selectedMaterials.All(x => x.Id != id))
        {
            _selectedMaterials.Add(m);
            _exportBase64 = null;
        }
    }

    private void Remove(Guid id)
    {
        _selectedMaterials.RemoveAll(x => x.Id == id);
        _exportBase64 = null;
    }

    private void ClearSelection()
    {
        _selectedMaterials.Clear();
        _exportBase64 = null;
    }

    private async Task ExportAsync()
    {
        _exporting = true;
        _exportError = null;
        try
        {
            _exportBase64 = await Materials.ExportAsync(_selectedMaterials.Select(x => x.Id).ToList());
            if (_exportBase64 == null)
                _exportError = L["material.share.export.failed"];
        }
        catch (HttpRequestException)
        {
            _exportError = L["material.share.export.failed"];
        }
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

    private async Task DownloadExportAsync()
    {
        if (_exportBase64 == null)
            return;
        await JS.InvokeVoidAsync("downloadHelper.saveAs", Encoding.UTF8.GetBytes(_exportBase64),
            $"spoolhero_materials_{DateTime.Now:yyyyMMdd}.txt");
    }

    private async Task OnImportFileAsync(InputFileChangeEventArgs e)
    {
        _importError = null;
        try
        {
            using var reader = new StreamReader(e.File.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024));
            _importBase64 = (await reader.ReadToEndAsync()).Trim();
        }
        catch (Exception)
        {
            _importError = L["material.share.import.file.error"];
        }
    }

    private async Task DoImportAsync()
    {
        _importing = true;
        _importError = null;
        _importSuccess = null;

        try
        {
            var resp = await Materials.ImportAsync(_importBase64.Trim());
            if (resp.IsSuccessStatusCode)
            {
                var result = await resp.Content.ReadFromJsonAsync<ImportResult>();
                _importSuccess = L.Format("material.import.success", result?.Imported ?? 0);
                _importBase64 = string.Empty;
            }
            else
            {
                _importError = L["material.share.import.invalid"];
            }
        }
        catch (HttpRequestException)
        {
            _importError = L["material.share.import.invalid"];
        }
        _importing = false;
    }

    private record ImportResult(int Imported);
}

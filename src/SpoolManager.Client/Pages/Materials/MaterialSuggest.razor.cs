using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Suggestions;
using SpoolManager.Shared.Models;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialSuggest
{
    [Inject] private SuggestionService Suggestions { get; set; } = default!;
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    [Parameter] public Guid? MaterialId { get; set; }

    private FilamentMaterialDto? _existingMaterial;
    private Guid? _loadedFor;
    private bool _loading;
    private bool _submitting;
    private bool _submitted;
    private bool _success;
    private string? _error;
    private List<MaterialSuggestionDto>? _mine;

    private string _type = string.Empty;
    private string _brand = string.Empty;
    private string _colorHex = "FFFFFF";
    private string? _colorName;
    private int _minTemp;
    private int _maxTemp;
    private int? _bedTemp;
    private decimal _diameter = 1.75m;
    private decimal? _density;
    private int? _dryTemp;
    private int? _dryHours;
    private string? _notes;
    private string? _reorderUrl;

    private string Title => MaterialId.HasValue ? L["material.suggest.title.fix"] : L["material.suggest.title.new"];

    private bool TempInvalid => _submitted && _minTemp > 0 && _maxTemp > 0 && _minTemp > _maxTemp;

    protected override async Task OnInitializedAsync()
    {
        try { _mine = (await Suggestions.GetMyAsync() ?? []).OrderByDescending(s => s.CreatedAt).ToList(); }
        catch (HttpRequestException) { _mine = []; }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!MaterialId.HasValue || _loadedFor == MaterialId)
            return;
        _loadedFor = MaterialId;
        _loading = true;
        try
        {
            _existingMaterial = await Materials.GetByIdAsync(MaterialId.Value);
        }
        catch (HttpRequestException)
        {
            _existingMaterial = null;
        }
        if (_existingMaterial != null)
        {
            _type = _existingMaterial.Type;
            _brand = _existingMaterial.Brand;
            _colorHex = _existingMaterial.ColorHex;
            _colorName = _existingMaterial.ColorName;
            _minTemp = _existingMaterial.MinTempCelsius;
            _maxTemp = _existingMaterial.MaxTempCelsius;
            _bedTemp = _existingMaterial.BedTempCelsius;
            _diameter = _existingMaterial.DiameterMm;
            _density = _existingMaterial.DensityGCm3;
            _dryTemp = _existingMaterial.DryTempCelsius;
            _dryHours = _existingMaterial.DryTimeHours;
            _notes = _existingMaterial.Notes;
            _reorderUrl = _existingMaterial.ReorderUrl;
        }
        _loading = false;
    }

    private void OnColorPick(ChangeEventArgs e) =>
        _colorHex = (e.Value?.ToString() ?? "#FFFFFF").TrimStart('#').ToUpperInvariant();

    private void OnHexInput(ChangeEventArgs e) =>
        _colorHex = (e.Value?.ToString() ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();

    private async Task SubmitAsync()
    {
        _submitted = true;
        _error = null;
        if (string.IsNullOrWhiteSpace(_type) || string.IsNullOrWhiteSpace(_brand)
            || !MaterialForm.IsValidHex(_colorHex) || TempInvalid)
            return;

        _submitting = true;
        var request = new CreateSuggestionRequest
        {
            MaterialId = MaterialId,
            Type = _type.Trim(),
            Brand = _brand.Trim(),
            ColorHex = _colorHex,
            ColorName = string.IsNullOrWhiteSpace(_colorName) ? null : _colorName.Trim(),
            MinTempCelsius = _minTemp,
            MaxTempCelsius = _maxTemp,
            BedTempCelsius = _bedTemp,
            DiameterMm = _diameter,
            DensityGCm3 = _density,
            DryTempCelsius = _dryTemp,
            DryTimeHours = _dryHours,
            Notes = _notes,
            ReorderUrl = string.IsNullOrWhiteSpace(_reorderUrl) ? null : _reorderUrl.Trim()
        };
        try
        {
            var response = await Suggestions.CreateAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _success = true;
                _mine = (await Suggestions.GetMyAsync() ?? []).OrderByDescending(s => s.CreatedAt).ToList();
            }
            else
            {
                _error = L["material.suggest.failed"];
            }
        }
        catch (HttpRequestException)
        {
            _error = L["material.suggest.failed"];
        }
        _submitting = false;
    }

    private void ResetForNew()
    {
        _success = false;
        _submitted = false;
        _existingMaterial = null;
        _type = string.Empty;
        _brand = string.Empty;
        _colorHex = "FFFFFF";
        _colorName = null;
        _minTemp = 0;
        _maxTemp = 0;
        _bedTemp = null;
        _diameter = 1.75m;
        _density = null;
        _dryTemp = null;
        _dryHours = null;
        _notes = null;
        _reorderUrl = null;
        if (MaterialId.HasValue)
            Nav.NavigateTo("/materials/suggest");
    }

    private string StatusLabel(string status) => status switch
    {
        MaterialSuggestion.StatusApproved => L["material.suggest.status.approved"],
        MaterialSuggestion.StatusRejected => L["material.suggest.status.rejected"],
        _ => L["material.suggest.status.pending"]
    };

    private static string StatusClass(string status) => status switch
    {
        MaterialSuggestion.StatusApproved => "sh-chip-ok",
        MaterialSuggestion.StatusRejected => "sh-chip-danger",
        _ => "sh-chip-warn"
    };
}

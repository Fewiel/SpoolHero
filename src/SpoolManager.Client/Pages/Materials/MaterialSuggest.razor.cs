using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Suggestions;

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
    private bool _submitting;
    private bool _success;

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

    protected override async Task OnInitializedAsync()
    {
        if (MaterialId.HasValue)
        {
            _existingMaterial = await Materials.GetByIdAsync(MaterialId.Value);
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
            }
        }
    }

    private async Task SubmitAsync()
    {
        _submitting = true;
        var request = new CreateSuggestionRequest
        {
            MaterialId = MaterialId,
            Type = _type,
            Brand = _brand,
            ColorHex = _colorHex,
            ColorName = _colorName,
            MinTempCelsius = _minTemp,
            MaxTempCelsius = _maxTemp,
            BedTempCelsius = _bedTemp,
            DiameterMm = _diameter,
            DensityGCm3 = _density,
            DryTempCelsius = _dryTemp,
            DryTimeHours = _dryHours,
            Notes = _notes
        };
        var response = await Suggestions.CreateAsync(request);
        _submitting = false;
        if (response.IsSuccessStatusCode)
            _success = true;
    }
}

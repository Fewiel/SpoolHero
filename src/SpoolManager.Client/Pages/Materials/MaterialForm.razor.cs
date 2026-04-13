using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialForm
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    [Parameter] public Guid? Id { get; set; }

    private static readonly (string hex, string name)[] ColorPresets =
    [
        ("1A1A1A","Schwarz"),("F2F2F2","Weiß"),("808080","Grau"),("C0C0C0","Silber"),
        ("FF0000","Rot"),("8B0000","Dunkelrot"),("FF6400","Orange"),("FFFF00","Gelb"),
        ("00FF00","Grün"),("006400","Dunkelgrün"),("0000FF","Blau"),("ADD8E6","Hellblau"),
        ("800080","Lila"),("FF69B4","Pink"),("8B4513","Braun"),("FFE4C4","Natur")
    ];

    private bool _loading = true;
    private bool _saving;
    private string? _error;
    private CreateMaterialRequest _form = new();

    private string _diameterStr
    {
        get => _form.DiameterMm.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                _form.DiameterMm = d;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        if (Id.HasValue)
        {
            var m = await Materials.GetByIdAsync(Id.Value);
            if (m != null)
            {
                _form = new CreateMaterialRequest
                {
                    Type = m.Type, ColorHex = m.ColorHex, Brand = m.Brand,
                    MinTempCelsius = m.MinTempCelsius, MaxTempCelsius = m.MaxTempCelsius,
                    ColorName = m.ColorName, DiameterMm = m.DiameterMm, WeightGrams = m.WeightGrams,
                    BedTempCelsius = m.BedTempCelsius, DensityGCm3 = m.DensityGCm3,
                    DryTempCelsius = m.DryTempCelsius, DryTimeHours = m.DryTimeHours,
                    Notes = m.Notes, ReorderUrl = m.ReorderUrl, PricePerKg = m.PricePerKg,
                    IsPublic = m.IsPublic
                };
            }
        }
        _loading = false;
    }

    private void OnColorPick(ChangeEventArgs e)
    {
        var hex = e.Value?.ToString()?.TrimStart('#').ToUpper() ?? "FFFFFF";
        _form.ColorHex = hex;
    }

    private async Task SaveAsync()
    {
        _saving = true;
        _error = null;

        var resp = Id.HasValue
            ? await Materials.UpdateAsync(Id.Value, new UpdateMaterialRequest
            {
                Type = _form.Type, ColorHex = _form.ColorHex.ToUpper(), Brand = _form.Brand,
                MinTempCelsius = _form.MinTempCelsius, MaxTempCelsius = _form.MaxTempCelsius,
                ColorName = _form.ColorName, DiameterMm = _form.DiameterMm, WeightGrams = _form.WeightGrams,
                BedTempCelsius = _form.BedTempCelsius, DensityGCm3 = _form.DensityGCm3,
                DryTempCelsius = _form.DryTempCelsius, DryTimeHours = _form.DryTimeHours,
                Notes = _form.Notes, ReorderUrl = _form.ReorderUrl, PricePerKg = _form.PricePerKg,
                IsPublic = _form.IsPublic
            })
            : await Materials.CreateAsync(new CreateMaterialRequest
            {
                Type = _form.Type, ColorHex = _form.ColorHex.ToUpper(), Brand = _form.Brand,
                MinTempCelsius = _form.MinTempCelsius, MaxTempCelsius = _form.MaxTempCelsius,
                ColorName = _form.ColorName, DiameterMm = _form.DiameterMm, WeightGrams = _form.WeightGrams,
                BedTempCelsius = _form.BedTempCelsius, DensityGCm3 = _form.DensityGCm3,
                DryTempCelsius = _form.DryTempCelsius, DryTimeHours = _form.DryTimeHours,
                Notes = _form.Notes, ReorderUrl = _form.ReorderUrl, PricePerKg = _form.PricePerKg,
                IsPublic = _form.IsPublic
            });

        _saving = false;
        if (resp.IsSuccessStatusCode)
            Nav.NavigateTo("/materials");
        else _error = await resp.Content.ReadAsStringAsync();
    }
}

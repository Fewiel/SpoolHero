using System.Globalization;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminMaterialForm
{
    [Inject] private AdminMaterialService AdminMaterials { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public Guid? Id { get; set; }

    private static readonly string[] CommonTypes =
        ["PLA", "PLA+", "PETG", "ABS", "ASA", "TPU", "Nylon", "PC", "HIPS", "PVA", "Wood", "Metal"];

    private static readonly (string hex, string key)[] ColorPresets =
    [
        ("1A1A1A", "admin.materials.color.black"), ("F2F2F2", "admin.materials.color.white"),
        ("808080", "admin.materials.color.grey"), ("C0C0C0", "admin.materials.color.silver"),
        ("FF0000", "admin.materials.color.red"), ("8B0000", "admin.materials.color.darkred"),
        ("FF6400", "admin.materials.color.orange"), ("FFFF00", "admin.materials.color.yellow"),
        ("00FF00", "admin.materials.color.green"), ("006400", "admin.materials.color.darkgreen"),
        ("0000FF", "admin.materials.color.blue"), ("ADD8E6", "admin.materials.color.lightblue"),
        ("800080", "admin.materials.color.purple"), ("FF69B4", "admin.materials.color.pink"),
        ("8B4513", "admin.materials.color.brown"), ("FFE4C4", "admin.materials.color.natural")
    ];

    private bool _loading = true;
    private bool _saving;
    private bool _notFound;
    private string? _error;
    private readonly Dictionary<string, string> _errors = [];
    private CreateMaterialRequest _form = new() { ColorHex = "1A1A1A", MinTempCelsius = 190, MaxTempCelsius = 220 };

    private string _diameterStr
    {
        get => _form.DiameterMm.ToString("0.00", CultureInfo.InvariantCulture);
        set
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                _form.DiameterMm = d;
        }
    }

    private bool HexValid => _form.ColorHex.Length == 6 && _form.ColorHex.All(Uri.IsHexDigit);

    private string PickerValue => "#" + (HexValid ? _form.ColorHex.ToLowerInvariant() : "ffffff");

    private string Invalid(string field) => _errors.ContainsKey(field) ? "is-invalid" : string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        if (Id.HasValue)
        {
            try
            {
                var all = await AdminMaterials.GetAllAsync();
                var m = all?.FirstOrDefault(x => x.Id == Id.Value);
                if (m != null)
                {
                    _form = new CreateMaterialRequest
                    {
                        Type = m.Type, ColorHex = m.ColorHex, Brand = m.Brand,
                        MinTempCelsius = m.MinTempCelsius, MaxTempCelsius = m.MaxTempCelsius,
                        ColorName = m.ColorName, DiameterMm = m.DiameterMm, WeightGrams = m.WeightGrams, SpoolWeightGrams = m.SpoolWeightGrams,
                        BedTempCelsius = m.BedTempCelsius, DensityGCm3 = m.DensityGCm3,
                        DryTempCelsius = m.DryTempCelsius, DryTimeHours = m.DryTimeHours,
                        Notes = m.Notes, ReorderUrl = m.ReorderUrl, PricePerKg = m.PricePerKg,
                        IsPublic = m.IsPublic
                    };
                }
                else
                {
                    _notFound = true;
                }
            }
            catch
            {
                _notFound = true;
            }
        }
        _loading = false;
    }

    private void OnColorPick(ChangeEventArgs e)
    {
        _form.ColorHex = e.Value?.ToString()?.TrimStart('#').ToUpperInvariant() ?? "FFFFFF";
        _errors.Remove("hex");
    }

    private void OnHexInput(ChangeEventArgs e)
    {
        _form.ColorHex = (e.Value?.ToString() ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        if (HexValid)
            _errors.Remove("hex");
    }

    private void PickPreset(string hex)
    {
        _form.ColorHex = hex;
        _errors.Remove("hex");
    }

    private bool Validate()
    {
        _errors.Clear();
        _form.Type = _form.Type?.Trim() ?? string.Empty;
        _form.Brand = _form.Brand?.Trim() ?? string.Empty;
        if (_form.Type.Length == 0)
            _errors["type"] = L["admin.materials.error.type"];
        if (_form.Brand.Length == 0)
            _errors["brand"] = L["admin.materials.error.brand"];
        if (!HexValid)
            _errors["hex"] = L["admin.materials.error.hex"];
        if (_form.MinTempCelsius < 0 || _form.MaxTempCelsius < 0 || _form.MinTempCelsius > _form.MaxTempCelsius)
            _errors["temp"] = L["admin.materials.error.temp"];
        if (!string.IsNullOrWhiteSpace(_form.ReorderUrl) &&
            !(Uri.TryCreate(_form.ReorderUrl.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)))
            _errors["url"] = L["admin.materials.error.url"];
        return _errors.Count == 0;
    }

    private async Task SaveAsync()
    {
        _error = null;
        if (!Validate())
        {
            _error = L["admin.materials.error.summary"];
            return;
        }

        _saving = true;
        try
        {
            var url = string.IsNullOrWhiteSpace(_form.ReorderUrl) ? null : _form.ReorderUrl.Trim();
            var colorName = string.IsNullOrWhiteSpace(_form.ColorName) ? null : _form.ColorName.Trim();
            HttpResponseMessage resp;
            if (Id.HasValue)
                resp = await AdminMaterials.UpdateAsync(Id.Value, new UpdateMaterialRequest
                {
                    Type = _form.Type, ColorHex = _form.ColorHex.ToUpperInvariant(), Brand = _form.Brand,
                    MinTempCelsius = _form.MinTempCelsius, MaxTempCelsius = _form.MaxTempCelsius,
                    ColorName = colorName, DiameterMm = _form.DiameterMm, WeightGrams = _form.WeightGrams, SpoolWeightGrams = _form.SpoolWeightGrams,
                    BedTempCelsius = _form.BedTempCelsius, DensityGCm3 = _form.DensityGCm3,
                    DryTempCelsius = _form.DryTempCelsius, DryTimeHours = _form.DryTimeHours,
                    Notes = _form.Notes, ReorderUrl = url, PricePerKg = _form.PricePerKg,
                    IsPublic = _form.IsPublic
                });
            else
                resp = await AdminMaterials.CreateAsync(new CreateMaterialRequest
                {
                    Type = _form.Type, ColorHex = _form.ColorHex.ToUpperInvariant(), Brand = _form.Brand,
                    MinTempCelsius = _form.MinTempCelsius, MaxTempCelsius = _form.MaxTempCelsius,
                    ColorName = colorName, DiameterMm = _form.DiameterMm, WeightGrams = _form.WeightGrams, SpoolWeightGrams = _form.SpoolWeightGrams,
                    BedTempCelsius = _form.BedTempCelsius, DensityGCm3 = _form.DensityGCm3,
                    DryTempCelsius = _form.DryTempCelsius, DryTimeHours = _form.DryTimeHours,
                    Notes = _form.Notes, ReorderUrl = url, PricePerKg = _form.PricePerKg,
                    IsPublic = _form.IsPublic
                });

            if (resp.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/admin/materials");
                return;
            }
            _error = L["admin.materials.save.failed"];
        }
        catch
        {
            _error = L["admin.materials.save.failed"];
        }
        _saving = false;
    }
}

using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialForm
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    [Parameter] public Guid? Id { get; set; }
    [Parameter] public Guid? CloneId { get; set; }

    private string Title => Id.HasValue ? L["material.edit"] : CloneId.HasValue ? L["material.clone"] : L["material.add"];

    private string Subtitle => Id.HasValue ? L["material.form.subtitle.edit"]
        : CloneId.HasValue ? L["material.form.subtitle.clone"]
        : L["material.form.subtitle.new"];

    private static readonly (string Hex, string Key)[] ColorPresets =
    [
        ("1A1A1A", "material.color.preset.black"), ("F2F2F2", "material.color.preset.white"),
        ("808080", "material.color.preset.grey"), ("C0C0C0", "material.color.preset.silver"),
        ("FF0000", "material.color.preset.red"), ("8B0000", "material.color.preset.darkred"),
        ("FF6400", "material.color.preset.orange"), ("FFFF00", "material.color.preset.yellow"),
        ("00FF00", "material.color.preset.green"), ("006400", "material.color.preset.darkgreen"),
        ("0000FF", "material.color.preset.blue"), ("ADD8E6", "material.color.preset.lightblue"),
        ("800080", "material.color.preset.purple"), ("FF69B4", "material.color.preset.pink"),
        ("8B4513", "material.color.preset.brown"), ("FFE4C4", "material.color.preset.natural")
    ];

    /// <summary>Typical values per type, used to pre-fill empty fields when a type chip is picked.</summary>
    private static readonly Dictionary<string, (int Min, int Max, int Bed, decimal Density, int DryTemp, int DryHours)> TypePresets = new()
    {
        ["PLA"] = (190, 220, 60, 1.24m, 50, 6),
        ["PETG"] = (220, 250, 80, 1.27m, 65, 6),
        ["ABS"] = (230, 260, 100, 1.04m, 80, 4),
        ["ASA"] = (240, 260, 100, 1.07m, 80, 4),
        ["TPU"] = (210, 235, 50, 1.21m, 55, 8),
        ["PA"] = (250, 280, 80, 1.14m, 80, 12),
        ["PC"] = (260, 300, 110, 1.20m, 80, 8)
    };

    private bool _loading = true;
    private bool _saving;
    private bool _submitted;
    private bool _notFound;
    private bool _readOnlyGlobal;
    private string? _error;
    private string? _defaultsApplied;
    private CreateMaterialRequest _form = new();

    private string? BrandError => string.IsNullOrWhiteSpace(_form.Brand) ? L["material.form.error.brand"] : null;
    private string? TypeError => string.IsNullOrWhiteSpace(_form.Type) ? L["material.form.error.type"] : null;
    private string? HexError => IsValidHex(_form.ColorHex) ? null : L["material.form.error.hex"];
    private string? TempError => _form.MinTempCelsius < 0 || _form.MaxTempCelsius < 0
        || (_form.MinTempCelsius > 0 && _form.MaxTempCelsius > 0 && _form.MinTempCelsius > _form.MaxTempCelsius)
        ? L["material.form.error.temp"] : null;
    private string? UrlError => string.IsNullOrWhiteSpace(_form.ReorderUrl)
        || (Uri.TryCreate(_form.ReorderUrl.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
        ? null : L["material.form.error.url"];

    private bool IsValid => BrandError == null && TypeError == null && HexError == null && TempError == null && UrlError == null;

    private bool ShowError(string? error) => _submitted && error != null;

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        var sourceId = Id ?? CloneId;
        if (sourceId.HasValue)
        {
            FilamentMaterialDto? m = null;
            try { m = await Materials.GetByIdAsync(sourceId.Value); }
            catch (HttpRequestException) { }

            if (m == null)
            {
                _notFound = true;
                _loading = false;
                return;
            }

            if (Id.HasValue && m.IsGlobal)
                _readOnlyGlobal = true;

            _form = new CreateMaterialRequest
            {
                Type = m.Type, ColorHex = m.ColorHex, Brand = m.Brand,
                MinTempCelsius = m.MinTempCelsius, MaxTempCelsius = m.MaxTempCelsius,
                ColorName = m.ColorName, DiameterMm = m.DiameterMm, WeightGrams = m.WeightGrams, SpoolWeightGrams = m.SpoolWeightGrams,
                BedTempCelsius = m.BedTempCelsius, DensityGCm3 = m.DensityGCm3,
                DryTempCelsius = m.DryTempCelsius, DryTimeHours = m.DryTimeHours,
                Notes = m.Notes, ReorderUrl = m.ReorderUrl, PricePerKg = m.PricePerKg,
                IsPublic = m.IsPublic && !CloneId.HasValue
            };
        }
        _loading = false;
    }

    internal static bool IsValidHex(string? hex) =>
        hex is { Length: 6 } && hex.All(Uri.IsHexDigit);

    private void OnColorPick(ChangeEventArgs e)
    {
        _form.ColorHex = (e.Value?.ToString() ?? "#FFFFFF").TrimStart('#').ToUpperInvariant();
    }

    private void OnHexInput(ChangeEventArgs e)
    {
        _form.ColorHex = (e.Value?.ToString() ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
    }

    private void PickPreset(string hex, string nameKey)
    {
        var previousPresetName = ColorPresets.Select(p => L[p.Key]).FirstOrDefault(n => n == _form.ColorName);
        _form.ColorHex = hex;
        // Fill the colour name only if it is empty or still the name of another preset.
        if (string.IsNullOrWhiteSpace(_form.ColorName) || previousPresetName != null)
            _form.ColorName = L[nameKey];
    }

    private void PickType(string type)
    {
        _form.Type = type;
        _defaultsApplied = null;
        if (!TypePresets.TryGetValue(type, out var d))
            return;

        var applied = false;
        if (_form.MinTempCelsius <= 0) { _form.MinTempCelsius = d.Min; applied = true; }
        if (_form.MaxTempCelsius <= 0) { _form.MaxTempCelsius = d.Max; applied = true; }
        if (_form.BedTempCelsius is null or <= 0) { _form.BedTempCelsius = d.Bed; applied = true; }
        if (_form.DensityGCm3 is null or <= 0) { _form.DensityGCm3 = d.Density; applied = true; }
        if (_form.DryTempCelsius is null or <= 0) { _form.DryTempCelsius = d.DryTemp; applied = true; }
        if (_form.DryTimeHours is null or <= 0) { _form.DryTimeHours = d.DryHours; applied = true; }
        if (applied)
            _defaultsApplied = type;
    }

    private async Task SaveAsync()
    {
        _submitted = true;
        _error = null;
        if (!IsValid)
            return;

        _saving = true;
        var request = new UpdateMaterialRequest
        {
            Type = _form.Type.Trim(), ColorHex = _form.ColorHex.ToUpperInvariant(), Brand = _form.Brand.Trim(),
            MinTempCelsius = _form.MinTempCelsius, MaxTempCelsius = _form.MaxTempCelsius,
            ColorName = string.IsNullOrWhiteSpace(_form.ColorName) ? null : _form.ColorName.Trim(),
            DiameterMm = _form.DiameterMm, WeightGrams = _form.WeightGrams, SpoolWeightGrams = _form.SpoolWeightGrams,
            BedTempCelsius = _form.BedTempCelsius, DensityGCm3 = _form.DensityGCm3,
            DryTempCelsius = _form.DryTempCelsius, DryTimeHours = _form.DryTimeHours,
            Notes = string.IsNullOrWhiteSpace(_form.Notes) ? null : _form.Notes,
            ReorderUrl = string.IsNullOrWhiteSpace(_form.ReorderUrl) ? null : _form.ReorderUrl.Trim(),
            PricePerKg = _form.PricePerKg,
            IsPublic = _form.IsPublic
        };

        try
        {
            var resp = Id.HasValue
                ? await Materials.UpdateAsync(Id.Value, request)
                : await Materials.CreateAsync(request);

            if (resp.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/materials");
                return;
            }
            _error = L["material.form.save.failed"];
        }
        catch (HttpRequestException)
        {
            _error = L["material.form.save.failed"];
        }
        finally
        {
            _saving = false;
        }
    }
}

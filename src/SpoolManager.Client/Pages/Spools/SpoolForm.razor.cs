using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;
using SpoolManager.Shared.Helpers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpoolManager.Client.Pages.Spools;

public partial class SpoolForm
{
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "material")] public Guid? PresetMaterialId { get; set; }
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private string? _error;
    private bool _submitted;
    private bool _notFound;
    private CreateSpoolRequest _form = new() { RemainingPercent = 100 };
    private FilamentMaterialDto? _selectedMaterial;
    private List<PrinterDto> _printers = [];
    private List<StorageLocationDto> _storages = [];
    private List<DryerDto> _dryerList = [];
    private List<string> _owners = [];
    private decimal? _grossWeight;

    private decimal? EffectiveTare => SpoolWeightCalculator.EffectiveTare(_form.EmptySpoolWeightGrams, _selectedMaterial?.SpoolWeightGrams);

    private bool _nfcAvailable;
    private bool _nfcReading;
    private bool _nfcImportSuccess;
    private string? _nfcImportError;
    private DotNetObjectReference<SpoolForm>? _dotnetRef;

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        var pt = Printers.GetAllAsync();
        var st = Storage.GetAllAsync();
        var dt = Dryers.GetAllAsync();
        var ot = Spools.GetOwnersAsync();
        await Task.WhenAll(pt, st, dt, ot);
        _printers = await pt ?? [];
        _storages = await st ?? [];
        _dryerList = await dt ?? [];
        _owners = await ot ?? [];

        if (Id.HasValue)
        {
            var spool = await Spools.GetByIdAsync(Id.Value);
            if (spool != null)
            {
                _form.FilamentMaterialId = spool.FilamentMaterialId;
                _form.RemainingWeightGrams = spool.RemainingWeightGrams;
                _form.RemainingPercent = spool.RemainingPercent;
                _form.PrinterId = spool.PrinterId;
                _form.StorageLocationId = spool.StorageLocationId;
                _form.DryerId = spool.DryerId;
                _form.PurchasedAt = spool.PurchasedAt;
                _form.PurchasePrice = spool.PurchasePrice;
                _form.RfidTagUid = spool.RfidTagUid;
                _form.ReorderUrl = spool.ReorderUrl;
                _form.Notes = spool.Notes;
                _form.OwnerName = spool.OwnerName;
                _form.EmptySpoolWeightGrams = spool.EmptySpoolWeightGrams;
                _selectedMaterial = await Materials.GetByIdAsync(spool.FilamentMaterialId);
            }
            else
            {
                _notFound = true;
            }
        }
        else
        {
            _nfcAvailable = await Nfc.CheckSupportAsync();
            if (PresetMaterialId is { } preset)
            {
                try
                {
                    var mat = await Materials.GetByIdAsync(preset);
                    if (mat != null)
                        OnMaterialPicked(mat);
                }
                catch (Exception)
                {
                    // invalid preset – the user simply picks a material
                }
            }
        }
        _loading = false;
    }

    private async Task StartNfcScanAsync()
    {
        _nfcImportError = null;
        _nfcImportSuccess = false;
        _nfcReading = true;
        _dotnetRef ??= DotNetObjectReference.Create(this);
        try { await Nfc.StartReadAsync(_dotnetRef); }
        catch { _nfcReading = false; _nfcImportError = L["spool.nfc.error"]; }
    }

    private async Task StopNfcScanAsync()
    {
        _nfcReading = false;
        try { await Nfc.StopReadAsync(); } catch { }
    }

    [JSInvokable]
    public async void OnTagRead(string json)
    {
        _nfcReading = false;
        await ApplyTagJsonAsync(json);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyTagJsonAsync(string json)
    {
        _nfcImportError = null;
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var brand = root.TryGetProperty("brand", out var b) ? b.GetString() : null;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var colorHex = root.TryGetProperty("color_hex", out var c) ? c.GetString()?.TrimStart('#') : null;

            if (!string.IsNullOrEmpty(brand) || !string.IsNullOrEmpty(type))
            {
                var searchQ = $"{brand} {type}".Trim();
                var candidates = await Http.GetFromJsonAsync<List<FilamentMaterialDto>>(
                    $"api/materials/search?q={Uri.EscapeDataString(searchQ)}&limit=20") ?? [];

                var match = candidates.FirstOrDefault(m =>
                    string.Equals(m.Brand, brand, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Type, type, StringComparison.OrdinalIgnoreCase) &&
                    (colorHex == null || string.Equals(m.ColorHex, colorHex, StringComparison.OrdinalIgnoreCase)));

                match ??= candidates.FirstOrDefault(m =>
                    string.Equals(m.Brand, brand, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Type, type, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    _selectedMaterial = match;
                    _form.FilamentMaterialId = match.Id;
                    if (match.WeightGrams is > 0)
                    {
                        _form.RemainingWeightGrams = match.WeightGrams.Value;
                        _form.RemainingPercent = 100;
                    }
                    if (string.IsNullOrEmpty(_form.ReorderUrl) && match.ReorderUrl != null)
                        _form.ReorderUrl = match.ReorderUrl;
                    _nfcImportSuccess = true;
                }
                else
                {
                    _nfcImportError = L["spool.nfc.import.no.match"]
                        .Replace("{brand}", brand ?? "?")
                        .Replace("{type}", type ?? "?");
                }
            }
            else
            {
                _nfcImportError = L["spool.nfc.import.invalid"];
            }
        }
        catch
        {
            _nfcImportError = L["spool.nfc.import.invalid"];
        }
    }

    private async Task ImportFileAsync(InputFileChangeEventArgs e)
    {
        _nfcImportError = null;
        _nfcImportSuccess = false;
        try
        {
            using var stream = e.File.OpenReadStream(maxAllowedSize: 64 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            string? json;
            if (e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                json = System.Text.Encoding.UTF8.GetString(bytes);
            }
            else
            {
                var decoded = await Tags.DecodeAsync(new TagDecodeRequest { Base64 = Convert.ToBase64String(bytes) });
                json = decoded?.RawJson;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _nfcImportError = L["spool.nfc.import.invalid"];
                return;
            }
            await ApplyTagJsonAsync(json);
        }
        catch
        {
            _nfcImportError = L["spool.nfc.import.invalid"];
        }
    }

    [JSInvokable]
    public void OnReadError(string error)
    {
        _nfcReading = false;
        _nfcImportError = L["spool.nfc.error"];
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnScanStarted() => InvokeAsync(StateHasChanged);

    [JSInvokable]
    public void OnWriteSuccess() { }

    [JSInvokable]
    public void OnWriteError(string error) { }

    private void OnMaterialPicked(FilamentMaterialDto? mat)
    {
        if (mat == null)
            return;
        _selectedMaterial = mat;
        _form.FilamentMaterialId = mat.Id;
        if (Id.HasValue)
        {
            ApplyGrossWeight();
            return;
        }
        if (mat.WeightGrams is > 0)
        {
            _form.RemainingWeightGrams = mat.WeightGrams.Value;
            _form.RemainingPercent = 100;
        }
        if (string.IsNullOrEmpty(_form.ReorderUrl) && mat.ReorderUrl != null)
            _form.ReorderUrl = mat.ReorderUrl;
        ApplyGrossWeight();
    }

    private void SetWeight(decimal value)
    {
        _form.RemainingWeightGrams = value;
        if (_selectedMaterial?.WeightGrams is > 0)
            _form.RemainingPercent = Math.Round(value / _selectedMaterial.WeightGrams.Value * 100m, 1);
    }

    private void SetGrossWeight(decimal? value)
    {
        _grossWeight = value;
        ApplyGrossWeight();
    }

    private void SetTare(decimal? value)
    {
        _form.EmptySpoolWeightGrams = value;
        ApplyGrossWeight();
    }

    private void ApplyGrossWeight()
    {
        if (_grossWeight is not { } gross || EffectiveTare is not { } tare)
            return;
        SetWeight(SpoolWeightCalculator.RemainingFromGross(gross, tare));
    }

    private void SetPercent(decimal value)
    {
        _form.RemainingPercent = value;
        if (_selectedMaterial?.WeightGrams is > 0)
            _form.RemainingWeightGrams = Math.Round(value / 100m * _selectedMaterial.WeightGrams.Value, 1);
    }

    // ── Validation ────────────────────────────────────────────────────────
    private bool MaterialInvalid => _form.FilamentMaterialId == Guid.Empty;
    private bool GramsInvalid => _form.RemainingWeightGrams < 0;
    private bool PercentInvalid => _form.RemainingPercent is < 0 or > 100;
    private bool PriceInvalid => _form.PurchasePrice is < 0;
    private bool ReorderInvalid => !string.IsNullOrWhiteSpace(_form.ReorderUrl)
        && !(Uri.TryCreate(_form.ReorderUrl.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps));
    private bool HasErrors => MaterialInvalid || GramsInvalid || PercentInvalid || PriceInvalid || ReorderInvalid;

    private string Invalid(bool invalid) => _submitted && invalid ? "is-invalid" : string.Empty;

    private string CurrentLocationKey =>
        _form.PrinterId != null ? "p:" + _form.PrinterId :
        _form.StorageLocationId != null ? "s:" + _form.StorageLocationId :
        _form.DryerId != null ? "d:" + _form.DryerId : "none";

    private void SetLocation(string key)
    {
        Guid? id = key.Length > 2 && Guid.TryParse(key[2..], out var g) ? g : null;
        _form.PrinterId = key.StartsWith("p:") ? id : null;
        _form.StorageLocationId = key.StartsWith("s:") ? id : null;
        _form.DryerId = key.StartsWith("d:") ? id : null;
    }

    private string CancelUrl => Id.HasValue ? $"/spools/{Id}" : "/spools";

    private async Task SaveAsync()
    {
        _submitted = true;
        _error = null;
        if (HasErrors)
        {
            _error = MaterialInvalid ? L["spool.material.required"] : L["spool.form.fix.errors"];
            return;
        }
        _saving = true;
        _form.ReorderUrl = string.IsNullOrWhiteSpace(_form.ReorderUrl) ? null : _form.ReorderUrl.Trim();
        _form.RfidTagUid = string.IsNullOrWhiteSpace(_form.RfidTagUid) ? null : _form.RfidTagUid.Trim();

        try
        {
            HttpResponseMessage resp;
            if (Id.HasValue)
            {
                resp = await Spools.UpdateAsync(Id.Value, new UpdateSpoolRequest
                {
                    FilamentMaterialId = _form.FilamentMaterialId,
                    RemainingWeightGrams = _form.RemainingWeightGrams,
                    RemainingPercent = _form.RemainingPercent,
                    RfidTagUid = _form.RfidTagUid,
                    PrinterId = _form.PrinterId,
                    StorageLocationId = _form.StorageLocationId,
                    DryerId = _form.DryerId,
                    PurchasedAt = _form.PurchasedAt,
                    PurchasePrice = _form.PurchasePrice,
                    ReorderUrl = _form.ReorderUrl,
                    Notes = _form.Notes,
                    OwnerName = _form.OwnerName,
                    EmptySpoolWeightGrams = _form.EmptySpoolWeightGrams
                });
            }
            else
            {
                resp = await Spools.CreateAsync(_form);
            }

            if (resp.IsSuccessStatusCode)
            {
                var saved = await ReadSpoolAsync(resp);
                Nav.NavigateTo(saved != null ? $"/spools/{saved.Id}" : Id.HasValue ? $"/spools/{Id}" : "/spools");
                return;
            }
            var body = await resp.Content.ReadAsStringAsync();
            _error = string.IsNullOrWhiteSpace(body) || body.Length > 300 ? L["spool.form.save.error"] : $"{L["spool.form.save.error"]} ({body})";
        }
        catch (Exception)
        {
            _error = L["spool.form.save.error"];
        }
        finally
        {
            _saving = false;
        }
    }

    private static async Task<SpoolDto?> ReadSpoolAsync(HttpResponseMessage resp)
    {
        try { return await resp.Content.ReadFromJsonAsync<SpoolDto>(); }
        catch (Exception) { return null; }
    }
}

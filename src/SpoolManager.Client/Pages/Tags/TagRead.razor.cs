using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Tags;

/// <summary>
/// Scan screen: reads a tag via Web NFC (or a .bin/.json tag file), shows what it belongs to and
/// offers the next actions. Scanning a spool and then a location (or vice versa) moves the spool.
/// </summary>
public partial class TagRead : IDisposable
{
    [Inject] private InventoryService Inventory { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private static readonly string[] LocationTypes = ["printer", "storage", "dryer"];

    private bool? _nfcSupported;
    private bool _scanning;
    private bool _busy;
    private bool _moving;
    private bool _disposed;
    private string? _error;
    private string? _moveMessage;
    private string? _lastSerial;
    private InventoryIdentifyResult? _current;
    private SpoolDto? _spool;
    private List<SpoolDto> _locationSpools = [];
    private ForeignTag? _foreign;
    private (InventoryIdentifyResult Spool, InventoryIdentifyResult Location)? _pendingMove;
    private string _moveTarget = string.Empty;
    private bool _locationsLoaded;
    private List<PrinterDto> _printers = [];
    private List<StorageLocationDto> _storage = [];
    private List<DryerDto> _dryers = [];
    private DotNetObjectReference<TagRead>? _objRef;

    private sealed record ForeignTag(string? Brand, string? Type, string ColorHex, int MinTemp, int MaxTemp);

    private string HeroState => _nfcSupported == false && _current == null ? "unsupported"
        : _scanning && _current == null ? "scanning"
        : _current == null ? "idle"
        : _current.EntityType == "unknown" ? "unknown"
        : "found";

    private string HeroIcon => HeroState switch
    {
        "unsupported" => "bi-phone",
        "found" => "bi-check-lg",
        "unknown" => "bi-question-lg",
        _ => "bi-broadcast"
    };

    private string HeroTitle => HeroState switch
    {
        "unsupported" => L["tag.read.state.unsupported"],
        "scanning" => L["tag.read.state.scanning"],
        "found" => L["tag.read.state.found"],
        "unknown" => L["tag.read.state.unknown"],
        _ => _nfcSupported == null ? L["common.loading"] : L["tag.read.state.idle"]
    };

    private string HeroText => HeroState switch
    {
        "unsupported" => L["tag.read.state.unsupported.text"],
        "scanning" => L["tag.read.state.scanning.text"],
        "found" when _current?.EntityType == "spool" => _scanning ? L["tag.read.state.found.spool"] : L["tag.read.state.found.text"],
        "found" => _scanning ? L["tag.read.state.found.location"] : L["tag.read.state.found.text"],
        "unknown" => _scanning ? L["tag.read.state.scanning.text"] : L["tag.read.state.unknown.text"],
        _ => L["tag.read.state.idle.text"]
    };

    protected override async Task OnInitializedAsync()
    {
        _nfcSupported = await Nfc.CheckSupportAsync();
    }

    private async Task ScanNfcAsync()
    {
        _error = null;
        _scanning = true;
        try
        {
            _objRef?.Dispose();
            _objRef = DotNetObjectReference.Create(this);
            await Nfc.StartReadAsync(_objRef);
        }
        catch (Exception ex)
        {
            _error = FriendlyNfcError(ex.Message);
            _scanning = false;
        }
    }

    private async Task StopScanAsync()
    {
        try { await Nfc.StopReadAsync(); } catch (JSException) { }
        _scanning = false;
    }

    [JSInvokable]
    public void OnScanStarted() { }

    [JSInvokable]
    public void OnReadError(string error)
    {
        if (_disposed)
            return;
        _error = FriendlyNfcError(error);
        // A read error of a single tag keeps the reader running; a failed start does not.
        if (error.Contains("NotAllowed", StringComparison.OrdinalIgnoreCase) || error.Contains("permission", StringComparison.OrdinalIgnoreCase))
            _scanning = false;
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnTagRead(string json, string serialNumber)
    {
        if (_disposed || _busy)
            return;
        await HandleTagAsync(string.IsNullOrWhiteSpace(json) ? null : json, string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber);
        StateHasChanged();
    }

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _error = null;
        try
        {
            await using var stream = e.File.OpenReadStream(maxAllowedSize: 64 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            string? json;
            if (e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || (bytes.Length > 0 && bytes[0] == (byte)'{'))
            {
                json = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                var decoded = await Tags.DecodeAsync(new TagDecodeRequest { Base64 = Convert.ToBase64String(bytes) });
                json = decoded?.RawJson;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _error = L["tag.read.file.invalid"];
                return;
            }
            // The tag file replaces any pending two-step move.
            _pendingMove = null;
            await HandleTagAsync(json, null);
        }
        catch (Exception)
        {
            _error = L["tag.read.file.invalid"];
        }
    }

    private async Task HandleTagAsync(string? json, string? serial)
    {
        _busy = true;
        _error = null;
        _moveMessage = null;
        StateHasChanged();
        try
        {
            var identified = await Inventory.IdentifyAsync(new InventoryIdentifyRequest { JsonPayload = json, SerialNumber = serial });
            if (identified == null)
            {
                _error = L["inventory.scan.identify.error"];
                return;
            }

            var previous = _current;

            // Two-step move: spool → location or location → spool
            if (previous != null && previous.EntityId != identified.EntityId)
            {
                if (previous.EntityType == "spool" && LocationTypes.Contains(identified.EntityType))
                {
                    _pendingMove = (previous, identified);
                    return;
                }
                if (LocationTypes.Contains(previous.EntityType) && identified.EntityType == "spool")
                {
                    _pendingMove = (identified, previous);
                    _spool = await TryGetSpoolAsync(identified.EntityId);
                    return;
                }
            }

            _pendingMove = null;
            await ShowAsync(identified, json, serial);
        }
        catch (Exception)
        {
            _error = L["inventory.scan.identify.error"];
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ShowAsync(InventoryIdentifyResult identified, string? json, string? serial)
    {
        _current = identified;
        _lastSerial = serial;
        _spool = null;
        _locationSpools = [];
        _foreign = null;
        _moveTarget = string.Empty;

        switch (identified.EntityType)
        {
            case "spool":
                _spool = await TryGetSpoolAsync(identified.EntityId);
                await EnsureLocationsAsync();
                break;
            case "printer":
                _locationSpools = await Spools.GetAllAsync(printerId: identified.EntityId, consumed: false) ?? [];
                break;
            case "storage":
                _locationSpools = await Spools.GetAllAsync(storageId: identified.EntityId, consumed: false) ?? [];
                break;
            case "dryer":
                _locationSpools = await Spools.GetAllAsync(dryerId: identified.EntityId, consumed: false) ?? [];
                break;
            default:
                _foreign = ParseForeign(json);
                break;
        }
    }

    private async Task<SpoolDto?> TryGetSpoolAsync(Guid id)
    {
        try { return await Spools.GetByIdAsync(id); }
        catch (HttpRequestException) { return null; }
    }

    private async Task EnsureLocationsAsync()
    {
        if (_locationsLoaded)
            return;
        _locationsLoaded = true;
        try
        {
            var printers = Printers.GetAllAsync();
            var storage = Storage.GetAllAsync();
            var dryers = Dryers.GetAllAsync();
            _printers = (await printers ?? []).OrderBy(p => p.Name).ToList();
            _storage = (await storage ?? []).OrderBy(s => s.Name).ToList();
            _dryers = (await dryers ?? []).OrderBy(d => d.Name).ToList();
        }
        catch (HttpRequestException)
        {
            _locationsLoaded = false;
        }
    }

    private void PrepareMoveFromSelect()
    {
        if (_current?.EntityType != "spool" || string.IsNullOrEmpty(_moveTarget))
            return;
        var parts = _moveTarget.Split(':');
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
            return;
        var name = parts[0] switch
        {
            "printer" => _printers.FirstOrDefault(p => p.Id == id)?.Name,
            "storage" => _storage.FirstOrDefault(s => s.Id == id)?.Name,
            "dryer" => _dryers.FirstOrDefault(d => d.Id == id)?.Name,
            _ => null
        } ?? string.Empty;
        _pendingMove = (_current, new InventoryIdentifyResult { EntityType = parts[0], EntityId = id, EntityName = name });
    }

    private async Task ConfirmMoveAsync()
    {
        if (_pendingMove == null)
            return;
        var (spool, location) = _pendingMove.Value;
        _moving = true;
        try
        {
            var result = await Inventory.PerformActionAsync(new InventoryActionRequest { First = spool, Second = location });
            if (result?.Success == true)
            {
                _pendingMove = null;
                await ShowAsync(spool, null, null);
                _moveMessage = L.Format("tag.read.move.done", spool.EntityName, location.EntityName);
            }
            else
            {
                _error = result?.Description ?? L["tag.read.move.failed"];
            }
        }
        catch (Exception)
        {
            _error = L["tag.read.move.failed"];
        }
        finally
        {
            _moving = false;
        }
    }

    private void CancelMove() => _pendingMove = null;

    private static ForeignTag? ParseForeign(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            string? Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int Int(string name) => root.TryGetProperty(name, out var v)
                ? v.ValueKind == JsonValueKind.Number ? v.GetInt32() : int.TryParse(v.GetString(), out var i) ? i : 0
                : 0;
            var type = Str("type");
            var brand = Str("brand");
            if (type == null && brand == null)
                return null;
            var hex = (Str("color_hex") ?? "FFFFFF").TrimStart('#');
            return new ForeignTag(brand, type, hex, Int("min_temp"), Int("max_temp"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string FriendlyNfcError(string raw) =>
        raw.Contains("NotAllowed", StringComparison.OrdinalIgnoreCase) || raw.Contains("permission", StringComparison.OrdinalIgnoreCase)
            ? L["tag.read.error.permission"]
            : $"{L["tag.read.error.read"]} ({raw})";

    private string SpoolPlace(SpoolDto s) =>
        s.PrinterName != null ? L.Format("tag.read.place.printer", s.PrinterName)
        : s.DryerName != null ? L.Format("tag.read.place.dryer", s.DryerName)
        : s.StorageLocationName != null ? L.Format("tag.read.place.storage", s.StorageLocationName)
        : L["tag.read.place.none"];

    internal static string EntityIcon(string type) => type switch
    {
        "spool" => "bi-record-circle",
        "printer" => "bi-printer",
        "storage" => "bi-box-seam",
        "dryer" => "bi-thermometer-sun",
        _ => "bi-question-lg"
    };

    private static string LocationListUrl(string type) => type switch
    {
        "printer" => "/printers",
        "dryer" => "/dryers",
        _ => "/storage"
    };

    public void Dispose()
    {
        _disposed = true;
        if (_scanning)
        {
            try { _ = Nfc.StopReadAsync(); } catch (Exception) { }
        }
        _objRef?.Dispose();
    }
}

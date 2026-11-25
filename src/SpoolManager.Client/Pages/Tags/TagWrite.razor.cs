using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Tags;

/// <summary>Write an NFC tag for a spool, printer, storage location or dryer (or download it as file).</summary>
public partial class TagWrite : IDisposable
{
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    /// <summary>Optional deep link: entity type (spool, printer, storage, dryer).</summary>
    [SupplyParameterFromQuery(Name = "type")] public string? QueryType { get; set; }

    /// <summary>Optional deep link: entity id to preselect.</summary>
    [SupplyParameterFromQuery(Name = "id")] public Guid? QueryId { get; set; }

    private static readonly string[] Kinds = ["spool", "printer", "storage", "dryer"];
    private const int MaxSpools = 40;

    private sealed record Item(Guid Id, string Title, string? Sub, string? ColorHex, decimal Percent, bool HasTag);

    private bool? _nfcSupported;
    private string _kind = "spool";
    private bool _loading = true;
    private bool _preparing;
    private bool _disposed;
    private string _search = string.Empty;
    private List<SpoolDto> _spools = [];
    private List<Item> _entityItems = [];
    private Item? _selected;
    private string _writeState = "idle";
    private string? _writeError;
    private DotNetObjectReference<TagWrite>? _objRef;
    private ElementReference _writeHeading;
    private bool _focusWrite;

    private IEnumerable<SpoolDto> FilteredSpools
    {
        get
        {
            IEnumerable<SpoolDto> q = _spools;
            foreach (var term in _search.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = term;
                q = q.Where(s => s.MaterialBrand.Contains(t, StringComparison.OrdinalIgnoreCase)
                                 || s.MaterialType.Contains(t, StringComparison.OrdinalIgnoreCase)
                                 || (s.MaterialColorName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                                 || (s.OwnerName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            return q;
        }
    }

    private int FilteredSpoolCount => FilteredSpools.Count();

    private List<Item> Items => _kind == "spool"
        ? FilteredSpools.Take(MaxSpools).Select(ToItem).ToList()
        : _entityItems;

    private string AddUrl => _kind switch
    {
        "spool" => "/spools/new",
        "printer" => "/printers",
        "dryer" => "/dryers",
        _ => "/storage"
    };

    protected override async Task OnInitializedAsync()
    {
        _nfcSupported = await Nfc.CheckSupportAsync();
        if (QueryType != null && Kinds.Contains(QueryType))
            _kind = QueryType;
        await LoadAsync();
        if (QueryId.HasValue)
            _selected = Items.FirstOrDefault(i => i.Id == QueryId.Value);
    }

    private async Task SetKindAsync(string kind)
    {
        if (_kind == kind)
            return;
        _kind = kind;
        _selected = null;
        _writeState = "idle";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            switch (_kind)
            {
                case "spool":
                    if (_spools.Count == 0)
                        _spools = (await Spools.GetAllAsync(consumed: false) ?? [])
                            .OrderBy(s => s.MaterialBrand).ThenBy(s => s.MaterialType).ThenBy(s => s.MaterialColorName).ToList();
                    break;
                case "printer":
                    _entityItems = (await Printers.GetAllAsync() ?? [])
                        .OrderBy(p => p.Name).Select(p => new Item(p.Id, p.Name, p.Notes, null, 0, !string.IsNullOrEmpty(p.RfidTagUid))).ToList();
                    break;
                case "storage":
                    _entityItems = (await Storage.GetAllAsync() ?? [])
                        .OrderBy(s => s.Name).Select(s => new Item(s.Id, s.Name, s.Description, null, 0, !string.IsNullOrEmpty(s.RfidTagUid))).ToList();
                    break;
                case "dryer":
                    _entityItems = (await Dryers.GetAllAsync() ?? [])
                        .OrderBy(d => d.Name).Select(d => new Item(d.Id, d.Name, d.Description, null, 0, !string.IsNullOrEmpty(d.RfidTagUid))).ToList();
                    break;
            }
        }
        catch (HttpRequestException)
        {
            _entityItems = [];
        }
        _loading = false;
    }

    private Item ToItem(SpoolDto s) => new(
        s.Id,
        $"{s.MaterialBrand} {s.MaterialType}",
        $"{s.MaterialColorName ?? "#" + s.MaterialColorHex} · {Math.Round(s.RemainingWeightGrams)} g" + (s.OwnerName != null ? $" · {s.OwnerName}" : string.Empty),
        s.MaterialColorHex,
        s.RemainingPercent,
        !string.IsNullOrEmpty(s.RfidTagUid));

    private void Select(Item item)
    {
        _selected = item;
        _writeState = "idle";
        _writeError = null;
        _focusWrite = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusWrite)
            return;
        _focusWrite = false;
        // Brings step 2 into view on phones, where it sits below the list.
        try { await _writeHeading.FocusAsync(); } catch (JSException) { }
    }

    private async Task<TagEncodeResponse?> EncodeAsync()
    {
        if (_selected == null)
            return null;
        return _kind == "spool"
            ? await Tags.EncodeAsync(new TagEncodeRequest { SpoolId = _selected.Id })
            : await Tags.EncodeEntityAsync(new TagEncodeEntityRequest { EntityType = _kind, EntityId = _selected.Id });
    }

    private async Task WriteAsync()
    {
        if (_selected == null)
            return;
        _preparing = true;
        _writeError = null;
        try
        {
            var encoded = await EncodeAsync();
            if (encoded?.JsonPayload == null)
            {
                _writeState = "error";
                _writeError = L["tag.write.state.error.encode"];
                return;
            }
            _objRef?.Dispose();
            _objRef = DotNetObjectReference.Create(this);
            _writeState = "writing";
            _preparing = false;
            StateHasChanged();
            await Nfc.WriteAsync(encoded.JsonPayload, _objRef, encoded.SpoolmanId);
        }
        catch (Exception ex)
        {
            _writeState = "error";
            _writeError = ex.Message;
        }
        finally
        {
            _preparing = false;
        }
    }

    private async Task CancelWriteAsync()
    {
        // Web NFC has no abort for write() in our helper; reloading the reader releases it.
        try { await Nfc.StopReadAsync(); } catch (JSException) { }
        _writeState = "idle";
    }

    [JSInvokable]
    public void OnWriteSuccess()
    {
        if (_disposed || _writeState != "writing")
            return;
        _writeState = "done";
        StateHasChanged();
    }

    [JSInvokable]
    public void OnWriteError(string message)
    {
        if (_disposed || _writeState != "writing")
            return;
        _writeState = "error";
        _writeError = message;
        StateHasChanged();
    }

    private async Task DownloadBinAsync()
    {
        if (_selected == null)
            return;
        if (_kind == "spool")
            await Tags.DownloadSpoolBinAsync(_selected.Id);
        else
            await Tags.DownloadEntityBinAsync(_kind, _selected.Id);
    }

    private async Task DownloadJsonAsync()
    {
        if (_selected == null)
            return;
        if (_kind == "spool")
            await Tags.DownloadSpoolJsonAsync(_selected.Id);
        else
            await Tags.DownloadEntityJsonAsync(_kind, _selected.Id);
    }

    public void Dispose()
    {
        _disposed = true;
        _objRef?.Dispose();
    }
}

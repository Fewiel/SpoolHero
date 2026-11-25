using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Components.Places;

/// <summary>
/// Shared page body for printers, storage locations and dryers: a card per place with its image,
/// notes and the spools currently in it, plus create/edit/delete dialogs and NFC tag actions.
/// </summary>
public partial class PlaceBoard : IDisposable
{
    private const long MaxImageBytes = 8_388_608;
    private const int MaxRings = 8;

    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Kind-specific API access (printer, storage or dryer).</summary>
    [Parameter, EditorRequired] public IPlaceApi Api { get; set; } = default!;

    /// <summary>Bootstrap icon class used for the kind (e.g. bi-printer).</summary>
    [Parameter, EditorRequired] public string Icon { get; set; } = "bi-geo-alt";

    private string Kind => Api.Kind;
    private bool HasProject => Project.CurrentProject != null;
    private bool IsAdmin => Project.CurrentProject?.MyRole == "admin";
    private string NotesLabelKey => Kind == "printer" ? "printer.notes" : $"{Kind}.description";

    private bool _loading = true;
    private bool _loadError;
    private List<PlaceItem> _items = [];
    private List<SpoolDto> _spools = [];

    private PlaceItem? _form;
    private Guid? _editId;
    private string? _existingImageSrc;
    private byte[]? _pendingImage;
    private string? _pendingImageName;
    private string? _pendingImageType;
    private bool _removeImage;
    private string? _formError;
    private bool _saving;

    private PlaceItem? _deleteTarget;
    private string? _deleteError;
    private bool _deleting;

    private PlaceItem? _jsonTarget;
    private bool _jsonCopied;

    private Guid? _tagTargetId;
    private string? _tagMessage;
    private bool _tagSuccess;

    private Guid? _dryingId;
    private string? _notice;
    private bool _noticeSuccess;

    private DotNetObjectReference<PlaceBoard>? _selfRef;
    private Timer? _clock;

    protected override async Task OnInitializedAsync()
    {
        if (!HasProject)
        {
            _loading = false;
            return;
        }
        await LoadAsync();
        if (Kind == "dryer")
            _clock = new Timer(_ => InvokeAsync(StateHasChanged), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private async Task LoadAsync()
    {
        // Only show the spinner on the first load; refreshes keep the cards on screen.
        _loading = _items.Count == 0;
        _loadError = false;
        try
        {
            var itemsTask = Api.GetAllAsync();
            var spoolsTask = Spools.GetAllAsync(consumed: false);
            await Task.WhenAll(itemsTask, spoolsTask);
            _items = itemsTask.Result.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            _spools = spoolsTask.Result ?? [];
        }
        catch (HttpRequestException)
        {
            _loadError = true;
        }
        _loading = false;
    }

    private List<SpoolDto> SpoolsIn(Guid placeId) => Kind switch
    {
        "printer" => _spools.Where(s => s.PrinterId == placeId).ToList(),
        "storage" => _spools.Where(s => s.StorageLocationId == placeId).ToList(),
        _ => _spools.Where(s => s.DryerId == placeId).ToList()
    };

    // ── Create / edit ───────────────────────────────────────────────────────
    private string? FormImageSrc =>
        _pendingImage != null ? $"data:{_pendingImageType};base64,{Convert.ToBase64String(_pendingImage)}"
        : _removeImage ? null
        : _existingImageSrc;

    private void ShowAdd()
    {
        ResetForm();
        _form = new PlaceItem();
    }

    private void ShowEdit(PlaceItem item)
    {
        ResetForm();
        _editId = item.Id;
        _form = item.Clone();
        _existingImageSrc = item.ImageBase64 != null ? $"data:{item.ImageContentType};base64,{item.ImageBase64}" : null;
    }

    private void CancelForm()
    {
        if (_saving)
            return;
        _form = null;
        ResetForm();
    }

    private void ResetForm()
    {
        _editId = null;
        _existingImageSrc = null;
        _pendingImage = null;
        _pendingImageName = null;
        _pendingImageType = null;
        _removeImage = false;
        _formError = null;
    }

    private async Task PickImageAsync(InputFileChangeEventArgs e)
    {
        _formError = null;
        var file = e.File;
        if (file.Size > MaxImageBytes)
        {
            _formError = L["common.image.too.large"];
            return;
        }
        using var ms = new MemoryStream();
        await using var stream = file.OpenReadStream(MaxImageBytes);
        await stream.CopyToAsync(ms);
        _pendingImage = ms.ToArray();
        _pendingImageName = file.Name;
        _pendingImageType = string.IsNullOrEmpty(file.ContentType) ? "image/png" : file.ContentType;
        _removeImage = false;
    }

    private void RemoveImage()
    {
        _pendingImage = null;
        _pendingImageName = null;
        _pendingImageType = null;
        _removeImage = true;
    }

    private async Task SaveAsync()
    {
        if (_form == null || _saving)
            return;
        _form.Name = _form.Name.Trim();
        _form.Notes = string.IsNullOrWhiteSpace(_form.Notes) ? null : _form.Notes.Trim();
        _form.RfidTagUid = string.IsNullOrWhiteSpace(_form.RfidTagUid) ? null : _form.RfidTagUid.Trim();
        if (_form.Name.Length == 0)
        {
            _formError = L["place.name.required"];
            return;
        }

        _saving = true;
        _formError = null;
        try
        {
            var resp = _editId.HasValue
                ? await Api.UpdateAsync(_editId.Value, _form)
                : await Api.CreateAsync(_form);
            if (!resp.IsSuccessStatusCode)
            {
                _formError = await ReadErrorAsync(resp);
                return;
            }

            var id = _editId ?? await ReadIdAsync(resp);
            var imageOk = true;
            if (id.HasValue && _pendingImage != null)
            {
                using var content = new MultipartFormDataContent();
                var bytes = new ByteArrayContent(_pendingImage);
                bytes.Headers.ContentType = new MediaTypeHeaderValue(_pendingImageType ?? "application/octet-stream");
                content.Add(bytes, "file", _pendingImageName ?? "image");
                imageOk = (await Api.UploadImageAsync(id.Value, content)).IsSuccessStatusCode;
            }
            else if (id.HasValue && _removeImage && _existingImageSrc != null)
            {
                imageOk = (await Api.DeleteImageAsync(id.Value)).IsSuccessStatusCode;
            }

            _form = null;
            ResetForm();
            await LoadAsync();
            if (!imageOk)
                ShowNotice(L["place.image.error"], false);
        }
        catch (HttpRequestException)
        {
            _formError = L["place.save.error"];
        }
        finally
        {
            _saving = false;
        }
    }

    private static async Task<Guid?> ReadIdAsync(HttpResponseMessage resp)
    {
        try
        {
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("id", out var id) && id.TryGetGuid(out var guid))
                return guid;
        }
        catch (JsonException) { }
        return null;
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage resp) =>
        resp.StatusCode == System.Net.HttpStatusCode.Forbidden
            ? L["place.no.permission"]
            : await ResponseMessages.ReadAsync(resp, L["place.save.error"]);

    // ── Delete ──────────────────────────────────────────────────────────────
    private void AskDelete(PlaceItem item)
    {
        _deleteError = null;
        _deleteTarget = item;
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        _deleting = true;
        _deleteError = null;
        try
        {
            var resp = await Api.DeleteAsync(_deleteTarget.Id);
            if (!resp.IsSuccessStatusCode)
            {
                _deleteError = await ReadErrorAsync(resp);
                return;
            }
            var name = _deleteTarget.Name;
            _deleteTarget = null;
            await LoadAsync();
            ShowNotice(L.Format("place.deleted", name), true);
        }
        catch (HttpRequestException)
        {
            _deleteError = L["place.save.error"];
        }
        finally
        {
            _deleting = false;
        }
    }

    // ── Drying ──────────────────────────────────────────────────────────────
    private enum DryState { Idle, Running, Done, Unknown }

    private readonly record struct DryInfo(DryState State, double Percent, TimeSpan Remaining);

    /// <summary>
    /// Drying starts when the spool is marked as dried (DriedAt). A finished run older than a day is
    /// treated as "not drying" so that stale dates do not show up as freshly dried.
    /// </summary>
    private static DryInfo Drying(SpoolDto s)
    {
        if (s.DriedAt == null)
            return new(DryState.Idle, 0, TimeSpan.Zero);
        var started = DateTime.SpecifyKind(s.DriedAt.Value, DateTimeKind.Utc);
        var elapsed = DateTime.UtcNow - started;
        if (s.MaterialDryTimeHours is not > 0)
            return elapsed > TimeSpan.FromDays(1) ? new(DryState.Idle, 0, TimeSpan.Zero) : new(DryState.Unknown, 0, TimeSpan.Zero);
        var total = TimeSpan.FromHours(s.MaterialDryTimeHours.Value);
        if (elapsed >= total + TimeSpan.FromDays(1))
            return new(DryState.Idle, 0, TimeSpan.Zero);
        if (elapsed >= total)
            return new(DryState.Done, 100, TimeSpan.Zero);
        return new(DryState.Running, Math.Clamp(elapsed / total * 100, 0, 100), total - elapsed);
    }

    private async Task StartDryingAsync(SpoolDto s)
    {
        _dryingId = s.Id;
        try
        {
            var resp = await Spools.MarkDriedAsync(s.Id);
            if (resp.IsSuccessStatusCode)
            {
                _spools = await Spools.GetAllAsync(consumed: false) ?? _spools;
                ShowNotice(L.Format("place.drying.started.notice", SpoolName(s)), true);
            }
            else
                ShowNotice(L["place.save.error"], false);
        }
        catch (HttpRequestException)
        {
            ShowNotice(L["place.save.error"], false);
        }
        finally
        {
            _dryingId = null;
        }
    }

    private string Duration(TimeSpan t)
    {
        var minutes = (int)Math.Ceiling(t.TotalMinutes);
        var h = minutes / 60;
        var m = minutes % 60;
        return h > 0 ? L.Format("place.duration.hm", h, m) : L.Format("place.duration.m", m);
    }

    private static string FormatTime(DateTime? dt) =>
        dt == null ? "–" : DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");

    // ── NFC tags ────────────────────────────────────────────────────────────
    private async Task WriteTagAsync(PlaceItem item)
    {
        _tagTargetId = item.Id;
        _tagMessage = null;
        if (!await Nfc.CheckSupportAsync())
        {
            _tagMessage = L["place.tag.nfc.unsupported"];
            _tagSuccess = false;
            return;
        }
        var encoded = await Tags.EncodeEntityAsync(new TagEncodeEntityRequest { EntityType = Kind, EntityId = item.Id });
        if (encoded?.JsonPayload == null)
        {
            _tagMessage = L["common.error"];
            _tagSuccess = false;
            return;
        }
        _tagMessage = L["tag.nfc.scanning"];
        _tagSuccess = true;
        _selfRef ??= DotNetObjectReference.Create(this);
        await Nfc.WriteAsync(encoded.JsonPayload, _selfRef);
    }

    [JSInvokable] public void OnWriteSuccess() { _tagMessage = L["tag.write.success"]; _tagSuccess = true; StateHasChanged(); }
    [JSInvokable] public void OnWriteError(string message) { _tagMessage = message; _tagSuccess = false; StateHasChanged(); }
    [JSInvokable] public void OnScanStarted() { }
    [JSInvokable] public void OnTagRead(string json, string serialNumber) { }
    [JSInvokable] public void OnReadError(string message) { }

    private void ShowJson(PlaceItem item)
    {
        _jsonCopied = false;
        _jsonTarget = item;
    }

    private string JsonPayload(PlaceItem item) =>
        $"{{\"protocol\":\"spoolmanager\",\"type\":\"{Kind}\",\"id\":\"{item.Id}\"}}";

    private async Task CopyJsonAsync()
    {
        if (_jsonTarget == null)
            return;
        try { _jsonCopied = await JS.InvokeAsync<bool>("clipboardHelper.copy", JsonPayload(_jsonTarget)); }
        catch (JSException) { _jsonCopied = false; }
    }

    // ── Texts ───────────────────────────────────────────────────────────────
    private static string SpoolName(SpoolDto s) =>
        string.Join(" ", new[] { s.MaterialBrand, s.MaterialType, s.MaterialColorName }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private string SpoolLabel(SpoolDto s) => L.Format("place.spool.label", SpoolName(s), (int)Math.Round(s.RemainingPercent));

    private string SpoolCountText(int count) => count switch
    {
        0 => L["place.spools.count.zero"],
        1 => L["place.spools.count.one"],
        _ => L.Format("place.spools.count", count)
    };

    private string PlaceSubtitle(List<SpoolDto> spools)
    {
        var text = SpoolCountText(spools.Count);
        if (Kind != "dryer")
            return text;
        var states = spools.Select(Drying).ToList();
        var running = states.Where(d => d.State == DryState.Running).ToList();
        if (running.Count > 0)
            return $"{text} · {L.Format("place.drying.ready.in", Duration(running.Max(d => d.Remaining)))}";
        if (states.Any(d => d.State == DryState.Done))
            return $"{text} · {L["place.drying.ready"]}";
        return text;
    }

    private static string ShortUid(string uid) => uid.Length > 12 ? uid[..12] + "…" : uid;

    private void ShowNotice(string text, bool success)
    {
        _notice = text;
        _noticeSuccess = success;
    }

    public void Dispose()
    {
        _clock?.Dispose();
        _selfRef?.Dispose();
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Storage;

public partial class StorageList
{
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _loading = true;
    private List<StorageLocationDto> _locations = [];
    private StorageLocationDto? _deleteTarget;
    private CreateStorageLocationRequest? _form;
    private Guid? _editId;
    private string? _error;
    private string? _tagMessage;
    private bool _tagSuccess;
    private Guid? _tagLocationId;
    private string? _imageError;
    private bool _imageSuccess;
    private Guid? _imageTargetId;
    private Guid? _jsonTarget;
    private string? _jsonPayload;
    private bool _jsonCopied;

    [JSInvokable]
    public void OnWriteSuccess()
    {
        _tagMessage = L["tag.write.success"];
        _tagSuccess = true;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnWriteError(string message)
    {
        _tagMessage = message;
        _tagSuccess = false;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnScanStarted() { }

    [JSInvokable]
    public void OnTagRead(string json, string serialNumber) { }

    [JSInvokable]
    public void OnReadError(string message) { }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _locations = await Storage.GetAllAsync() ?? [];
        _loading = false;
    }

    private void ShowAdd()
    {
        _editId = null;
        _form = new CreateStorageLocationRequest();
        _error = null;
    }

    private void ShowEdit(StorageLocationDto loc)
    {
        _editId = loc.Id;
        _form = new CreateStorageLocationRequest { Name = loc.Name, Description = loc.Description, RfidTagUid = loc.RfidTagUid };
        _error = null;
    }

    private void Cancel()
    {
        _form = null;
        _editId = null;
        _error = null;
    }

    private async Task SaveAsync()
    {
        if (_form == null)
            return;
        HttpResponseMessage resp;
        if (_editId.HasValue)
            resp = await Storage.UpdateAsync(_editId.Value, new UpdateStorageLocationRequest { Name = _form.Name, Description = _form.Description, RfidTagUid = _form.RfidTagUid });
        else
            resp = await Storage.CreateAsync(_form);

        if (resp.IsSuccessStatusCode)
        {
            Cancel();
            await LoadAsync();
        }
        else
            _error = await resp.Content.ReadAsStringAsync();
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        await Storage.DeleteAsync(_deleteTarget.Id);
        _deleteTarget = null;
        await LoadAsync();
    }

    private async Task UploadImageAsync(Guid id, InputFileChangeEventArgs e)
    {
        _imageTargetId = id;
        _imageError = null;
        _imageSuccess = false;
        var file = e.File;
        if (file.Size > 8_388_608)
        {
            _imageError = L["common.image.too.large"];
            return;
        }
        using var ms = new MemoryStream();
        await using var stream = file.OpenReadStream(8_388_608);
        await stream.CopyToAsync(ms);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(ms.ToArray()), "file", file.Name);
        var resp = await Storage.UploadImageAsync(id, content);
        if (resp.IsSuccessStatusCode)
        {
            _imageError = null;
            _imageSuccess = true;
            await LoadAsync();
        }
        else
            _imageError = L["common.error"];
    }

    private async Task DeleteImageAsync(Guid id)
    {
        await Storage.DeleteImageAsync(id);
        await LoadAsync();
    }

    private async Task WriteTagAsync(StorageLocationDto loc)
    {
        _tagLocationId = loc.Id;
        _tagMessage = null;
        var supported = await Nfc.CheckSupportAsync();
        if (!supported)
        {
            _tagMessage = L["tag.nfc.not.supported"];
            _tagSuccess = false;
            return;
        }

        var encoded = await Tags.EncodeEntityAsync(new TagEncodeEntityRequest { EntityType = "storage", EntityId = loc.Id });
        if (encoded == null)
        {
            _tagMessage = L["common.error"];
            _tagSuccess = false;
            return;
        }

        await Nfc.WriteAsync(encoded.JsonPayload!, DotNetObjectReference.Create(this));
    }

    private async Task ShowEntityJsonAsync(Guid entityId)
    {
        if (_jsonTarget == entityId)
        {
            _jsonTarget = null;
            _jsonPayload = null;
            return;
        }
        _jsonCopied = false;
        _jsonPayload = $"{{\"protocol\":\"spoolmanager\",\"type\":\"storage\",\"id\":\"{entityId}\"}}";
        _jsonTarget = entityId;
    }

    private async Task CopyJsonAsync()
    {
        if (_jsonPayload == null)
            return;
        await JS.InvokeVoidAsync("clipboardHelper.copy", _jsonPayload);
        _jsonCopied = true;
    }

}

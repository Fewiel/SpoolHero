using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Printers;

public partial class PrinterList
{
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _loading = true;
    private List<PrinterDto> _printers = [];
    private PrinterDto? _deleteTarget;
    private CreatePrinterRequest? _form;
    private Guid? _editId;
    private string? _error;
    private string? _tagMessage;
    private bool _tagSuccess;
    private Guid? _tagPrinterId;
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
        _printers = await Printers.GetAllAsync() ?? [];
        _loading = false;
    }

    private void ShowAdd()
    {
        _editId = null;
        _form = new CreatePrinterRequest();
        _error = null;
    }

    private void ShowEdit(PrinterDto p)
    {
        _editId = p.Id;
        _form = new CreatePrinterRequest { Name = p.Name, Notes = p.Notes, RfidTagUid = p.RfidTagUid };
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
            resp = await Printers.UpdateAsync(_editId.Value, new UpdatePrinterRequest { Name = _form.Name, Notes = _form.Notes, RfidTagUid = _form.RfidTagUid });
        else
            resp = await Printers.CreateAsync(_form);

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
        await Printers.DeleteAsync(_deleteTarget.Id);
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
        var resp = await Printers.UploadImageAsync(id, content);
        if (resp.IsSuccessStatusCode)
        {
            _imageError = null;
            _imageSuccess = true;
            await LoadAsync();
        }
        else
        {
            _imageError = L["common.error"];
            _imageSuccess = false;
        }
    }

    private async Task DeleteImageAsync(Guid id)
    {
        await Printers.DeleteImageAsync(id);
        await LoadAsync();
    }

    private async Task WriteTagAsync(PrinterDto p)
    {
        _tagPrinterId = p.Id;
        _tagMessage = null;
        var supported = await Nfc.CheckSupportAsync();
        if (!supported)
        {
            _tagMessage = L["tag.nfc.not.supported"];
            _tagSuccess = false;
            return;
        }

        var encoded = await Tags.EncodeEntityAsync(new TagEncodeEntityRequest { EntityType = "printer", EntityId = p.Id });
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
        _jsonPayload = $"{{\"protocol\":\"spoolmanager\",\"type\":\"printer\",\"id\":\"{entityId}\"}}";
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

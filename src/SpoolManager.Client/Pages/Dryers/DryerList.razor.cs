using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Pages.Dryers;

public partial class DryerList
{
    [Inject] private DryerService Dryers { get; set; } = default!;
    [Inject] private TagService Tags { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private List<DryerDto> _dryers = [];
    private DryerDto? _deleteTarget;
    private CreateDryerRequest? _form;
    private Guid? _editId;
    private string? _error;
    private string? _tagMessage;
    private bool _tagSuccess;
    private Guid? _tagDryerId;
    private string? _imageError;
    private Guid? _imageTargetId;

    [JSInvokable] public void OnWriteSuccess() { _tagMessage = L["tag.write.success"]; _tagSuccess = true; StateHasChanged(); }
    [JSInvokable] public void OnWriteError(string message) { _tagMessage = message; _tagSuccess = false; StateHasChanged(); }
    [JSInvokable] public void OnScanStarted() { }
    [JSInvokable] public void OnTagRead(string json, string serialNumber) { }
    [JSInvokable] public void OnReadError(string message) { }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _dryers = await Dryers.GetAllAsync() ?? [];
        _loading = false;
    }

    private void ShowAdd() { _editId = null; _form = new CreateDryerRequest(); _error = null; }
    private void ShowEdit(DryerDto d) { _editId = d.Id; _form = new CreateDryerRequest { Name = d.Name, Description = d.Description, RfidTagUid = d.RfidTagUid }; _error = null; }
    private void Cancel() { _form = null; _editId = null; _error = null; }

    private async Task SaveAsync()
    {
        if (_form == null)
            return;
        HttpResponseMessage resp;
        if (_editId.HasValue)
            resp = await Dryers.UpdateAsync(_editId.Value, new UpdateDryerRequest { Name = _form.Name, Description = _form.Description, RfidTagUid = _form.RfidTagUid });
        else
            resp = await Dryers.CreateAsync(_form);
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
        await Dryers.DeleteAsync(_deleteTarget.Id);
        _deleteTarget = null;
        await LoadAsync();
    }

    private async Task UploadImageAsync(Guid id, InputFileChangeEventArgs e)
    {
        _imageTargetId = id;
        _imageError = null;
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
        var resp = await Dryers.UploadImageAsync(id, content);
        if (resp.IsSuccessStatusCode)
        {
            _imageError = null;
            await LoadAsync();
        }
        else
            _imageError = L["common.error"];
    }

    private async Task DeleteImageAsync(Guid id)
    {
        await Dryers.DeleteImageAsync(id);
        await LoadAsync();
    }

    private async Task WriteTagAsync(DryerDto d)
    {
        _tagDryerId = d.Id;
        _tagMessage = null;
        if (!await Nfc.CheckSupportAsync())
        {
            _tagMessage = L["tag.nfc.not.supported"];
            _tagSuccess = false;
            return;
        }
        var encoded = await Tags.EncodeEntityAsync(new TagEncodeEntityRequest { EntityType = "dryer", EntityId = d.Id });
        if (encoded?.JsonPayload == null)
        {
            _tagMessage = L["common.error"];
            _tagSuccess = false;
            return;
        }
        await Nfc.WriteAsync(encoded.JsonPayload, DotNetObjectReference.Create(this));
    }

}

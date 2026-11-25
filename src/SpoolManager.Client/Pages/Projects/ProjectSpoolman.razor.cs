using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Projects;
using SpoolManager.Shared.DTOs.Spoolman;

namespace SpoolManager.Client.Pages.Projects;

public partial class ProjectSpoolman
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private SpoolmanApiKeyService ApiKeys { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private ProjectDto? _project;
    private bool _canManage;
    private List<SpoolmanApiKeyDto> _keys = [];
    private bool _showCreate;
    private string _newKeyName = string.Empty;
    private bool _creating;
    private string? _error;
    private string? _createError;
    private Guid? _newKeyId;
    private SpoolmanApiKeyDto? _deleteTarget;
    private Guid? _copiedKeyId;

    private Guid? _expandedKeyId;
    private List<SpoolmanCallLogDto> _logs = [];
    private bool _logsLoading;

    private string BaseUrl => Nav.BaseUri.TrimEnd('/');
    private string ServerUrl(string apiKey) => $"{BaseUrl}/{apiKey}";
    private string ExampleServerUrl => _keys.Count > 0 ? ServerUrl(_keys[0].ApiKey) : ServerUrl("<API_KEY>");

    protected override async Task OnInitializedAsync()
    {
        var projects = await Project.GetMyProjectsAsync() ?? [];
        _project = projects.FirstOrDefault(p => p.Id == Id);
        if (_project == null)
        {
            Nav.NavigateTo("/projects");
            return;
        }

        _canManage = _project.MyRole is "admin" or "manager";
        if (_canManage)
            _keys = await ApiKeys.GetAllAsync(Id) ?? [];
        _loading = false;
    }

    private void OpenCreate()
    {
        _newKeyName = string.Empty;
        _createError = null;
        _showCreate = true;
    }

    private void CancelCreate()
    {
        if (_creating)
            return;
        _showCreate = false;
        _newKeyName = string.Empty;
        _createError = null;
    }

    private static DateTime AsLocal(DateTime dt) =>
        (dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt).ToLocalTime();

    private static string FormatTime(DateTime dt) => AsLocal(dt).ToString("g");

    private static string FormatClock(DateTime dt) => AsLocal(dt).ToString("HH:mm:ss");

    private async Task CreateKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(_newKeyName))
            return;
        _creating = true;
        _createError = null;
        SpoolmanApiKeyDto? created = null;
        try { created = await ApiKeys.CreateAsync(Id, _newKeyName.Trim()); }
        catch (HttpRequestException) { }
        _creating = false;
        if (created != null)
        {
            _keys.Insert(0, created);
            _newKeyId = created.Id;
            CancelCreate();
        }
        else
        {
            _createError = L["common.error"];
        }
    }

    private async Task DeleteKeyAsync()
    {
        if (_deleteTarget == null)
            return;
        var id = _deleteTarget.Id;
        _deleteTarget = null;
        _error = null;
        var response = await ApiKeys.DeleteAsync(Id, id);
        if (!response.IsSuccessStatusCode)
        {
            _error = L["common.error"];
            return;
        }
        _keys.RemoveAll(k => k.Id == id);
        if (_expandedKeyId == id)
            _expandedKeyId = null;
    }

    private async Task ToggleLogsAsync(Guid keyId)
    {
        if (_expandedKeyId == keyId)
        {
            _expandedKeyId = null;
            _logs = [];
            return;
        }

        _expandedKeyId = keyId;
        _logs = [];
        _logsLoading = true;
        StateHasChanged();

        _logs = await ApiKeys.GetLogsAsync(Id, keyId) ?? [];
        _logsLoading = false;
    }

    private async Task CopyAsync(Guid keyId, string text)
    {
        try
        {
            if (await JSRuntime.InvokeAsync<bool>("clipboardHelper.copy", text))
                _copiedKeyId = keyId;
        }
        catch
        {
            // Clipboard API unavailable (e.g. insecure context) - the URL field stays selectable.
        }
    }
}

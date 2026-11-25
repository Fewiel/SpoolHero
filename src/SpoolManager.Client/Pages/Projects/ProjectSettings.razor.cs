using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Projects;

namespace SpoolManager.Client.Pages.Projects;

public partial class ProjectSettings
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private InvitationService Invitations { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private ProjectDto? _project;
    private List<ProjectMemberDto> _members = [];
    private List<InvitationDto> _invitations = [];
    private string _myRole = "member";
    private Guid _myUserId;

    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _savingGeneral;
    private string? _generalError;

    private string _inviteRole = "member";
    private bool _creatingInvite;
    private string? _inviteError;
    private Guid? _newInviteId;
    private Guid? _copiedId;

    private ProjectMemberDto? _removeTarget;
    private bool _confirmLeave;
    private string? _leaveError;
    private bool _confirmDelete;
    private string _deleteConfirmText = string.Empty;
    private string? _deleteError;
    private bool _busy;

    private string? _notice;
    private bool _noticeSuccess;

    private bool IsAdmin => _myRole == "admin";
    private bool CanManage => _myRole is "admin" or "manager";
    private bool IsLastAdmin => IsAdmin && _members.Count(m => m.Role == "admin") <= 1;
    private bool IsCurrent => Project.CurrentProject?.Id == Id;

    private bool GeneralDirty =>
        _project != null && (_name.Trim() != _project.Name || (_description.Trim()) != (_project.Description ?? string.Empty));

    private List<InvitationDto> OpenInvitations =>
        _invitations.Where(i => !i.IsUsed && AsUtc(i.ExpiresAt) > DateTime.UtcNow).OrderByDescending(i => i.CreatedAt).ToList();

    protected override async Task OnInitializedAsync()
    {
        _myUserId = Auth.CurrentUser?.Id ?? Guid.Empty;
        var projects = await Project.GetMyProjectsAsync() ?? [];
        _project = projects.FirstOrDefault(p => p.Id == Id);
        if (_project == null)
        {
            Nav.NavigateTo("/projects");
            return;
        }
        _myRole = _project.MyRole;
        _name = _project.Name;
        _description = _project.Description ?? string.Empty;

        // Invitations are only visible to admins and managers (the API returns 403 for members).
        var mt = Project.GetMembersAsync(Id);
        var it = CanManage ? Invitations.GetByProjectAsync(Id) : Task.FromResult<List<InvitationDto>?>([]);
        await Task.WhenAll(mt, it);
        _members = await mt ?? [];
        _invitations = await it ?? [];

        _loading = false;
    }

    private static int RoleOrder(string role) => role switch { "admin" => 0, "manager" => 1, _ => 2 };

    private static string Initials(string name)
    {
        var parts = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        return name.Length == 0 ? "?" : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }

    private static DateTime AsUtc(DateTime dt) => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

    private static string FormatDate(DateTime dt) => AsUtc(dt).ToLocalTime().ToString("g");

    private void ShowNotice(string text, bool success)
    {
        _notice = text;
        _noticeSuccess = success;
    }

    private async Task SaveGeneralAsync()
    {
        if (_project == null || string.IsNullOrWhiteSpace(_name))
            return;
        _savingGeneral = true;
        _generalError = null;
        try
        {
            var request = new UpdateProjectRequest
            {
                Name = _name.Trim(),
                Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim()
            };
            var resp = await Project.UpdateAsync(Id, request);
            if (!resp.IsSuccessStatusCode)
            {
                _generalError = await ResponseMessages.ReadAsync(resp, L["common.error"]);
                return;
            }
            _project.Name = request.Name;
            _project.Description = request.Description;
            if (IsCurrent)
                await Project.SwitchProjectAsync(_project);
            ShowNotice(L["project.general.saved"], true);
        }
        catch (HttpRequestException)
        {
            _generalError = L["common.error"];
        }
        finally
        {
            _savingGeneral = false;
        }
    }

    private async Task CreateInviteAsync()
    {
        _creatingInvite = true;
        _inviteError = null;
        try
        {
            var before = _invitations.Select(i => i.Id).ToHashSet();
            var resp = await Invitations.CreateAsync(Id, new CreateInvitationRequest { Role = _inviteRole });
            if (resp.IsSuccessStatusCode)
            {
                _invitations = await Invitations.GetByProjectAsync(Id) ?? [];
                _newInviteId = _invitations.FirstOrDefault(i => !before.Contains(i.Id))?.Id;
            }
            else
                _inviteError = await ResponseMessages.ReadAsync(resp, L["common.error"]);
        }
        catch (HttpRequestException)
        {
            _inviteError = L["common.error"];
        }
        finally
        {
            _creatingInvite = false;
        }
    }

    private async Task ChangeRoleAsync(ProjectMemberDto m, string role)
    {
        var resp = await Project.UpdateMemberRoleAsync(Id, m.UserId, new UpdateMemberRoleRequest { Role = role });
        if (resp.IsSuccessStatusCode)
            ShowNotice(L.Format("project.member.role.changed", m.Username, L[$"project.role.{role}"]), true);
        else
            ShowNotice(await ResponseMessages.ReadAsync(resp, L["common.error"]), false);
        _members = await Project.GetMembersAsync(Id) ?? [];
    }

    private async Task RemoveMemberAsync()
    {
        if (_removeTarget == null)
            return;
        var target = _removeTarget;
        _removeTarget = null;
        var resp = await Project.RemoveMemberAsync(Id, target.UserId);
        if (resp.IsSuccessStatusCode)
            ShowNotice(L.Format("project.member.removed", target.Username), true);
        else
            ShowNotice(await ResponseMessages.ReadAsync(resp, L["common.error"]), false);
        _members = await Project.GetMembersAsync(Id) ?? [];
    }

    private async Task CopyTokenAsync(InvitationDto inv)
    {
        try
        {
            if (await JSRuntime.InvokeAsync<bool>("clipboardHelper.copy", inv.Token))
                _copiedId = inv.Id;
        }
        catch (JSException)
        {
            // Clipboard unavailable (e.g. insecure context); the code stays selectable in the field.
        }
    }

    private async Task LeaveAsync()
    {
        _busy = true;
        _leaveError = null;
        try
        {
            var resp = await Project.RemoveMemberAsync(Id, _myUserId);
            if (!resp.IsSuccessStatusCode)
            {
                _leaveError = await ResponseMessages.ReadAsync(resp, L["common.error"]);
                return;
            }
            await AfterProjectGoneAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private void OpenDelete()
    {
        _deleteConfirmText = string.Empty;
        _deleteError = null;
        _confirmDelete = true;
    }

    private async Task DeleteProjectAsync()
    {
        if (_project == null || _deleteConfirmText.Trim() != _project.Name.Trim())
            return;
        _busy = true;
        _deleteError = null;
        try
        {
            var resp = await Project.DeleteAsync(Id);
            if (!resp.IsSuccessStatusCode)
            {
                _deleteError = await ResponseMessages.ReadAsync(resp, L["common.error"]);
                return;
            }
            await AfterProjectGoneAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task AfterProjectGoneAsync()
    {
        if (IsCurrent)
            await Project.ClearProjectAsync();
        Nav.NavigateTo("/projects");
    }
}

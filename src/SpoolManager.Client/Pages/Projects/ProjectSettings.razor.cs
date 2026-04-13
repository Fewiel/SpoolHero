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
    private string _inviteRole = "member";
    private bool _creatingInvite;
    private ProjectMemberDto? _removeTarget;

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

        var mt = Project.GetMembersAsync(Id);
        var it = Invitations.GetByProjectAsync(Id);
        await Task.WhenAll(mt, it);
        _members = await mt ?? [];
        _invitations = await it ?? [];

        _loading = false;
    }

    private async Task CreateInviteAsync()
    {
        _creatingInvite = true;
        var resp = await Invitations.CreateAsync(Id, new CreateInvitationRequest { Role = _inviteRole });
        if (resp.IsSuccessStatusCode)
            _invitations = await Invitations.GetByProjectAsync(Id) ?? [];
        _creatingInvite = false;
    }

    private async Task ChangeRoleAsync(ProjectMemberDto m, string role)
    {
        await Project.UpdateMemberRoleAsync(Id, m.UserId, new UpdateMemberRoleRequest { Role = role });
        _members = await Project.GetMembersAsync(Id) ?? [];
    }

    private async Task RemoveMemberAsync()
    {
        if (_removeTarget == null)
            return;
        await Project.RemoveMemberAsync(Id, _removeTarget.UserId);
        _removeTarget = null;
        _members = await Project.GetMembersAsync(Id) ?? [];
    }

    private async Task CopyToken(string token)
    {
        try { await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", token); } catch { }
    }
}

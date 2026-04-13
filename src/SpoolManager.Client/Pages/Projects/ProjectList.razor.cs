using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Projects;

namespace SpoolManager.Client.Pages.Projects;

public partial class ProjectList
{
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private InvitationService Invitations { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private List<ProjectDto> _projects = [];
    private string _token = string.Empty;
    private InvitationInfoDto? _joinInfo;
    private string? _joinError;
    private bool _joining;

    protected override async Task OnInitializedAsync()
    {
        _projects = await Project.GetMyProjectsAsync() ?? [];
        _loading = false;
    }

    private async Task SwitchAsync(ProjectDto p)
    {
        await Project.SwitchProjectAsync(p);
        Nav.NavigateTo("/");
    }

    private async Task LookupTokenAsync()
    {
        _joinError = null;
        _joinInfo = null;
        var info = await Invitations.GetInfoAsync(_token.Trim());
        if (info == null || !info.IsValid)
            _joinError = info?.ErrorMessage ?? L["project.invite.invalid"];
        else
            _joinInfo = info;
    }

    private async Task AcceptInviteAsync()
    {
        _joining = true;
        _joinError = null;
        var joinedName = _joinInfo?.ProjectName;
        var resp = await Invitations.AcceptAsync(_token.Trim());
        _joining = false;
        if (resp.IsSuccessStatusCode)
        {
            _projects = await Project.GetMyProjectsAsync() ?? [];
            _joinInfo = null;
            _token = string.Empty;
            var joined = _projects.FirstOrDefault(p => p.Name == joinedName) ?? _projects.LastOrDefault();
            if (joined != null)
            {
                await Project.SwitchProjectAsync(joined);
                Nav.NavigateTo("/");
            }
        }
        else
        {
            _joinError = await resp.Content.ReadAsStringAsync();
        }
    }
}

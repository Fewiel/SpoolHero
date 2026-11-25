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
    private bool _loadError;
    private List<ProjectDto> _projects = [];
    private Guid? _switchingId;
    private string _token = string.Empty;
    private InvitationInfoDto? _joinInfo;
    private string? _joinError;
    private bool _checking;
    private bool _joining;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _projects = (await Project.GetMyProjectsAsync() ?? [])
                .OrderByDescending(p => p.Id == Project.CurrentProject?.Id)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (HttpRequestException)
        {
            _loadError = true;
        }
        _loading = false;
    }

    private static string Initial(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

    private async Task SwitchAsync(ProjectDto p)
    {
        _switchingId = p.Id;
        await Project.SwitchProjectAsync(p);
        Nav.NavigateTo("/");
    }

    private void ResetJoin()
    {
        _joinInfo = null;
        _joinError = null;
        _token = string.Empty;
    }

    private async Task LookupTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(_token))
            return;
        _checking = true;
        _joinError = null;
        _joinInfo = null;
        try
        {
            var info = await Invitations.GetInfoAsync(_token.Trim());
            if (info == null || !info.IsValid)
                _joinError = info?.ErrorMessage ?? L["project.invite.invalid"];
            else
                _joinInfo = info;
        }
        catch (HttpRequestException)
        {
            _joinError = L["project.invite.invalid"];
        }
        _checking = false;
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
            _joinError = await ResponseMessages.ReadAsync(resp, L["project.invite.invalid"]);
        }
    }
}

using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Projects;

namespace SpoolManager.Client.Pages.Projects;

public partial class ProjectCreate
{
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private CreateProjectRequest _form = new();
    private bool _saving;
    private bool _hasProjects;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        var projects = await Project.GetMyProjectsAsync();
        _hasProjects = projects is { Count: > 0 };
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Name)) { _error = L["project.name.required"]; return; }
        if (_form.Name.Length > 200) { _error = L["project.name.too.long"]; return; }
        _saving = true;
        _error = null;
        var resp = await Project.CreateAsync(_form);
        _saving = false;
        if (resp.IsSuccessStatusCode)
        {
            var projects = await Project.GetMyProjectsAsync() ?? [];
            var created = projects.LastOrDefault(p => p.Name == _form.Name);
            if (created != null) await Project.SwitchProjectAsync(created);
            Nav.NavigateTo("/");
        }
        else
        {
            _error = await resp.Content.ReadAsStringAsync();
        }
    }
}

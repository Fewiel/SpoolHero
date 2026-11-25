using System.Net.Http.Json;
using System.Text.Json;
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
        try
        {
            var projects = await Project.GetMyProjectsAsync();
            _hasProjects = projects is { Count: > 0 };
        }
        catch (HttpRequestException)
        {
            _hasProjects = true;
        }
    }

    private async Task SaveAsync()
    {
        _form.Name = (_form.Name ?? string.Empty).Trim();
        _form.Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim();
        if (string.IsNullOrWhiteSpace(_form.Name)) { _error = L["project.name.required"]; return; }
        if (_form.Name.Length > 200) { _error = L["project.name.too.long"]; return; }
        _saving = true;
        _error = null;
        try
        {
            var resp = await Project.CreateAsync(_form);
            if (!resp.IsSuccessStatusCode)
            {
                _error = await ResponseMessages.ReadAsync(resp, L["common.error"]);
                return;
            }

            ProjectDto? created = null;
            try { created = await resp.Content.ReadFromJsonAsync<ProjectDto>(); }
            catch (JsonException) { }
            if (created == null || created.Id == Guid.Empty)
            {
                var projects = await Project.GetMyProjectsAsync() ?? [];
                created = projects.LastOrDefault(p => p.Name == _form.Name);
            }
            if (created != null)
                await Project.SwitchProjectAsync(created);
            Nav.NavigateTo("/");
        }
        catch (HttpRequestException)
        {
            _error = L["common.error"];
        }
        finally
        {
            _saving = false;
        }
    }
}

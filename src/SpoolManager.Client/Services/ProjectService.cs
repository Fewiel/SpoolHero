using System.Net.Http.Json;
using Microsoft.JSInterop;
using SpoolManager.Shared.DTOs.Projects;

namespace SpoolManager.Client.Services;

public class ProjectService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public ProjectDto? CurrentProject { get; private set; }
    public event Action? OnProjectChanged;

    public ProjectService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", "project_id");
            if (!Guid.TryParse(stored, out var projectId)) return;

            var projects = await GetMyProjectsAsync();
            var match = projects?.FirstOrDefault(p => p.Id == projectId);
            if (match != null)
            {
                CurrentProject = match;
                SetProjectHeader(match.Id);
            }
        }
        catch { }
    }

    public Task<List<ProjectDto>?> GetMyProjectsAsync() =>
        _http.GetFromJsonAsync<List<ProjectDto>>("api/projects");

    public Task<HttpResponseMessage> CreateAsync(CreateProjectRequest request) =>
        _http.PostAsJsonAsync("api/projects", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateProjectRequest request) =>
        _http.PutAsJsonAsync($"api/projects/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _http.DeleteAsync($"api/projects/{id}");

    public Task<List<ProjectMemberDto>?> GetMembersAsync(Guid projectId) =>
        _http.GetFromJsonAsync<List<ProjectMemberDto>>($"api/projects/{projectId}/members");

    public Task<HttpResponseMessage> RemoveMemberAsync(Guid projectId, Guid userId) =>
        _http.DeleteAsync($"api/projects/{projectId}/members/{userId}");

    public Task<HttpResponseMessage> UpdateMemberRoleAsync(Guid projectId, Guid userId, UpdateMemberRoleRequest request) =>
        _http.PutAsJsonAsync($"api/projects/{projectId}/members/{userId}/role", request);

    public async Task SwitchProjectAsync(ProjectDto project)
    {
        CurrentProject = project;
        SetProjectHeader(project.Id);
        await _js.InvokeVoidAsync("localStorage.setItem", "project_id", project.Id.ToString());
        OnProjectChanged?.Invoke();
    }

    public async Task ClearProjectAsync()
    {
        CurrentProject = null;
        _http.DefaultRequestHeaders.Remove("X-Project-Id");
        try { await _js.InvokeVoidAsync("localStorage.removeItem", "project_id"); } catch { }
        OnProjectChanged?.Invoke();
    }

    private void SetProjectHeader(Guid projectId)
    {
        _http.DefaultRequestHeaders.Remove("X-Project-Id");
        _http.DefaultRequestHeaders.Add("X-Project-Id", projectId.ToString());
    }
}

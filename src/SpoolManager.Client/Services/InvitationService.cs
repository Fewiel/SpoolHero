using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Projects;

namespace SpoolManager.Client.Services;

public class InvitationService
{
    private readonly HttpClient _http;
    public InvitationService(HttpClient http) => _http = http;

    public Task<HttpResponseMessage> CreateAsync(Guid projectId, CreateInvitationRequest request) =>
        _http.PostAsJsonAsync($"api/projects/{projectId}/invitations", request);

    public Task<List<InvitationDto>?> GetByProjectAsync(Guid projectId) =>
        _http.GetFromJsonAsync<List<InvitationDto>>($"api/projects/{projectId}/invitations");

    public async Task<InvitationInfoDto?> GetInfoAsync(string token)
    {
        var response = await _http.GetAsync($"api/projects/invitations/info/{token}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<InvitationInfoDto>()
            : null;
    }

    public Task<HttpResponseMessage> AcceptAsync(string token) =>
        _http.PostAsJsonAsync("api/projects/join", new AcceptInvitationRequest { Token = token });
}

using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Spoolman;

namespace SpoolManager.Client.Services;

public class SpoolmanApiKeyService
{
    private readonly HttpClient _http;
    public SpoolmanApiKeyService(HttpClient http) => _http = http;

    public async Task<List<SpoolmanApiKeyDto>?> GetAllAsync(Guid projectId)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/spoolman/keys");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<SpoolmanApiKeyDto>>()
            : null;
    }

    public async Task<SpoolmanApiKeyDto?> CreateAsync(Guid projectId, string name)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/spoolman/keys", new CreateSpoolmanApiKeyRequest { Name = name });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SpoolmanApiKeyDto>()
            : null;
    }

    public Task<HttpResponseMessage> DeleteAsync(Guid projectId, Guid id) =>
        _http.DeleteAsync($"api/projects/{projectId}/spoolman/keys/{id}");

    public async Task<List<SpoolmanCallLogDto>?> GetLogsAsync(Guid projectId, Guid id)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/spoolman/keys/{id}/logs");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<SpoolmanCallLogDto>>()
            : null;
    }
}

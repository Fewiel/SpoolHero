using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Spoolman;

namespace SpoolManager.Client.Services;

public class SpoolmanApiKeyService
{
    private readonly HttpClient _http;
    public SpoolmanApiKeyService(HttpClient http) => _http = http;

    public Task<List<SpoolmanApiKeyDto>?> GetAllAsync() =>
        _http.GetFromJsonAsync<List<SpoolmanApiKeyDto>>("api/spoolman/apikeys");

    public async Task<SpoolmanApiKeyDto?> CreateAsync(string name)
    {
        var resp = await _http.PostAsJsonAsync("api/spoolman/apikeys", new CreateSpoolmanApiKeyRequest { Name = name });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<SpoolmanApiKeyDto>();
    }

    public Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _http.DeleteAsync($"api/spoolman/apikeys/{id}");

    public Task<List<SpoolmanCallLogDto>?> GetLogsAsync(Guid apiKeyId) =>
        _http.GetFromJsonAsync<List<SpoolmanCallLogDto>>($"api/spoolman/apikeys/{apiKeyId}/logs");
}

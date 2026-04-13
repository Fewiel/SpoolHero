using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Spools;

namespace SpoolManager.Client.Services;

public class SpoolService
{
    private readonly HttpClient _http;
    public SpoolService(HttpClient http) => _http = http;

    public Task<List<SpoolDto>?> GetAllAsync(Guid? materialId = null, Guid? printerId = null, Guid? storageId = null, Guid? dryerId = null, bool? consumed = null, string? search = null)
    {
        var query = BuildQuery(("materialId", materialId?.ToString()), ("printerId", printerId?.ToString()), ("storageId", storageId?.ToString()), ("dryerId", dryerId?.ToString()), ("consumed", consumed?.ToString().ToLower()), ("search", search));
        return _http.GetFromJsonAsync<List<SpoolDto>>($"api/spools{query}");
    }

    public Task<SpoolDto?> GetByIdAsync(Guid id) => _http.GetFromJsonAsync<SpoolDto>($"api/spools/{id}");

    public Task<HttpResponseMessage> CreateAsync(CreateSpoolRequest request) => _http.PostAsJsonAsync("api/spools", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateSpoolRequest request) => _http.PutAsJsonAsync($"api/spools/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/spools/{id}");

    public Task<HttpResponseMessage> MarkOpenedAsync(Guid id) => _http.PutAsJsonAsync($"api/spools/{id}/open", new { });

    public Task<HttpResponseMessage> MarkDriedAsync(Guid id) => _http.PutAsJsonAsync($"api/spools/{id}/dry", new { });

    public Task<HttpResponseMessage> MarkRepackagedAsync(Guid id) => _http.PutAsJsonAsync($"api/spools/{id}/repackage", new { });

    public Task<HttpResponseMessage> MarkReopenedAsync(Guid id) => _http.PutAsJsonAsync($"api/spools/{id}/reopen", new { });

    public Task<HttpResponseMessage> MarkConsumedAsync(Guid id) => _http.PutAsJsonAsync($"api/spools/{id}/consume", new { });

    public Task<HttpResponseMessage> UpdateRemainingAsync(Guid id, UpdateRemainingRequest request) => _http.PatchAsJsonAsync($"api/spools/{id}/remaining", request);

    private static string BuildQuery(params (string key, string? value)[] pairs)
    {
        var parts = pairs.Where(p => p.value != null).Select(p => $"{p.key}={Uri.EscapeDataString(p.value!)}").ToList();
        return parts.Any() ? "?" + string.Join("&", parts) : string.Empty;
    }
}

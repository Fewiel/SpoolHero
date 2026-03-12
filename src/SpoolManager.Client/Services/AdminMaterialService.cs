using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Services;

public class AdminMaterialService
{
    private readonly HttpClient _http;
    public AdminMaterialService(HttpClient http) => _http = http;

    public Task<List<FilamentMaterialDto>?> GetAllAsync(string? search = null)
    {
        var q = string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search)}";
        return _http.GetFromJsonAsync<List<FilamentMaterialDto>>($"api/admin/materials{q}");
    }

    public Task<HttpResponseMessage> CreateAsync(CreateMaterialRequest request) =>
        _http.PostAsJsonAsync("api/admin/materials", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateMaterialRequest request) =>
        _http.PutAsJsonAsync($"api/admin/materials/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _http.DeleteAsync($"api/admin/materials/{id}");
}

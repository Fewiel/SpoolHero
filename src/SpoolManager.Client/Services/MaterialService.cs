using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Services;

public class MaterialService
{
    private readonly HttpClient _http;
    private List<FilamentMaterialDto>? _cache;

    public MaterialService(HttpClient http) => _http = http;

    public async Task EnsureCacheAsync()
    {
        if (_cache != null)
            return;
        _cache = await _http.GetFromJsonAsync<List<FilamentMaterialDto>>("api/materials") ?? [];
    }

    public void InvalidateCache() => _cache = null;

    public async Task<List<FilamentMaterialDto>> GetAllCachedAsync()
    {
        await EnsureCacheAsync();
        return _cache!;
    }

    public async Task<List<FilamentMaterialDto>> SearchCachedAsync(string query)
    {
        await EnsureCacheAsync();
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<FilamentMaterialDto> result = _cache!;
        foreach (var term in terms)
        {
            var t = term;
            result = result.Where(m =>
                m.Brand.Contains(t, StringComparison.OrdinalIgnoreCase)
                || m.Type.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (m.ColorName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return result.ToList();
    }

    public async Task<PaginatedResult<MaterialSummaryDto>> GetPagedAsync(int page = 0, int pageSize = 50, string? type = null, string? brand = null, string? color = null)
    {
        var qs = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(type))
            qs.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(brand))
            qs.Add($"brand={Uri.EscapeDataString(brand)}");
        if (!string.IsNullOrWhiteSpace(color))
            qs.Add($"color={Uri.EscapeDataString(color)}");
        return await _http.GetFromJsonAsync<PaginatedResult<MaterialSummaryDto>>($"api/materials/paged?{string.Join("&", qs)}")
            ?? new PaginatedResult<MaterialSummaryDto>();
    }

    public Task<FilamentMaterialDto?> GetByIdAsync(Guid id) => _http.GetFromJsonAsync<FilamentMaterialDto>($"api/materials/{id}");

    public async Task<int> GetCountAsync()
    {
        var r = await _http.GetFromJsonAsync<CountResult>("api/materials/count");
        return r?.Count ?? 0;
    }
    private record CountResult(int Count);

    public async Task<HttpResponseMessage> CreateAsync(CreateMaterialRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/materials", request);
        if (resp.IsSuccessStatusCode)
            InvalidateCache();
        return resp;
    }

    public async Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateMaterialRequest request)
    {
        var resp = await _http.PutAsJsonAsync($"api/materials/{id}", request);
        if (resp.IsSuccessStatusCode)
            InvalidateCache();
        return resp;
    }

    public async Task<HttpResponseMessage> DeleteAsync(Guid id)
    {
        var resp = await _http.DeleteAsync($"api/materials/{id}");
        if (resp.IsSuccessStatusCode)
            InvalidateCache();
        return resp;
    }

    public async Task<string?> ExportAsync(List<Guid>? ids = null)
    {
        var q = ids != null ? $"?ids={string.Join(",", ids)}" : string.Empty;
        var response = await _http.GetFromJsonAsync<ExportResult>($"api/materials/export{q}");
        return response?.Base64;
    }

    public async Task<HttpResponseMessage> ImportAsync(string base64)
    {
        var resp = await _http.PostAsJsonAsync("api/materials/import", new { base64 });
        if (resp.IsSuccessStatusCode)
            InvalidateCache();
        return resp;
    }

    public async Task<(byte[] Data, string Filename)?> ExportOrcaAsync(List<Guid>? ids = null)
    {
        var q = ids is { Count: > 0 } ? $"?ids={string.Join(",", ids)}" : string.Empty;
        var response = await _http.GetAsync($"api/materials/export/orca{q}");
        if (!response.IsSuccessStatusCode)
            return null;
        var data = await response.Content.ReadAsByteArrayAsync();
        var filename = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? "orca_export.json";
        return (data, filename);
    }

    private class ExportResult { public string? Base64 { get; set; } }
}

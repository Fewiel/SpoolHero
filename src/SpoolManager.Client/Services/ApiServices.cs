using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;

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

public class MaterialService
{
    private readonly HttpClient _http;
    public MaterialService(HttpClient http) => _http = http;

    public Task<List<FilamentMaterialDto>?> GetAllAsync(string? search = null)
    {
        var q = string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search)}";
        return _http.GetFromJsonAsync<List<FilamentMaterialDto>>($"api/materials{q}");
    }

    public Task<FilamentMaterialDto?> GetByIdAsync(Guid id) => _http.GetFromJsonAsync<FilamentMaterialDto>($"api/materials/{id}");

    public Task<List<FilamentMaterialDto>?> SearchAsync(string query, int limit = 50) =>
        _http.GetFromJsonAsync<List<FilamentMaterialDto>>($"api/materials/search?q={Uri.EscapeDataString(query)}&limit={limit}");

    public async Task<int> GetCountAsync()
    {
        var r = await _http.GetFromJsonAsync<CountResult>("api/materials/count");
        return r?.Count ?? 0;
    }
    private record CountResult(int Count);

    public Task<HttpResponseMessage> CreateAsync(CreateMaterialRequest request) => _http.PostAsJsonAsync("api/materials", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateMaterialRequest request) => _http.PutAsJsonAsync($"api/materials/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/materials/{id}");

    public async Task<string?> ExportAsync(List<Guid>? ids = null)
    {
        var q = ids != null ? $"?ids={string.Join(",", ids)}" : string.Empty;
        var response = await _http.GetFromJsonAsync<ExportResult>($"api/materials/export{q}");
        return response?.Base64;
    }

    public Task<HttpResponseMessage> ImportAsync(string base64) => _http.PostAsJsonAsync("api/materials/import", new { base64 });

    public async Task<(byte[] Data, string Filename)?> ExportOrcaAsync(List<Guid>? ids = null)
    {
        var q = ids is { Count: > 0 } ? $"?ids={string.Join(",", ids)}" : string.Empty;
        var response = await _http.GetAsync($"api/materials/export/orca{q}");
        if (!response.IsSuccessStatusCode) return null;
        var data = await response.Content.ReadAsByteArrayAsync();
        var filename = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? "orca_export.json";
        return (data, filename);
    }

    private class ExportResult { public string? Base64 { get; set; } }
}

public class PrinterService
{
    private readonly HttpClient _http;
    public PrinterService(HttpClient http) => _http = http;

    public Task<List<PrinterDto>?> GetAllAsync() => _http.GetFromJsonAsync<List<PrinterDto>>("api/printers");

    public Task<HttpResponseMessage> CreateAsync(CreatePrinterRequest request) => _http.PostAsJsonAsync("api/printers", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdatePrinterRequest request) => _http.PutAsJsonAsync($"api/printers/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/printers/{id}");

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/printers/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/printers/{id}/image");
}

public class StorageService
{
    private readonly HttpClient _http;
    public StorageService(HttpClient http) => _http = http;

    public Task<List<StorageLocationDto>?> GetAllAsync() => _http.GetFromJsonAsync<List<StorageLocationDto>>("api/storage-locations");

    public Task<HttpResponseMessage> CreateAsync(CreateStorageLocationRequest request) => _http.PostAsJsonAsync("api/storage-locations", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateStorageLocationRequest request) => _http.PutAsJsonAsync($"api/storage-locations/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/storage-locations/{id}");

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/storage-locations/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/storage-locations/{id}/image");
}

public class TagService
{
    private readonly HttpClient _http;
    public TagService(HttpClient http) => _http = http;

    public Task<TagEncodeResponse?> EncodeAsync(TagEncodeRequest request) =>
        _http.PostAsJsonAsync("api/tags/encode", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagEncodeResponse>() : null).Unwrap();

    public Task<TagDecodeResponse?> DecodeAsync(TagDecodeRequest request) =>
        _http.PostAsJsonAsync("api/tags/decode", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagDecodeResponse>() : null).Unwrap();

    public Task<TagEncodeResponse?> EncodeEntityAsync(TagEncodeEntityRequest request) =>
        _http.PostAsJsonAsync("api/tags/encode-entity", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagEncodeResponse>() : null).Unwrap();

    public string GetDownloadUrl(Guid spoolId) => $"api/tags/download/{spoolId}";
}

public class InventoryService
{
    private readonly HttpClient _http;
    public InventoryService(HttpClient http) => _http = http;

    public async Task<InventoryIdentifyResult?> IdentifyAsync(InventoryIdentifyRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/inventory/identify", request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound) return null;
        return await response.Content.ReadFromJsonAsync<InventoryIdentifyResult>();
    }

    public async Task<InventoryActionResult?> PerformActionAsync(InventoryActionRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/inventory/action", request);
        return await response.Content.ReadFromJsonAsync<InventoryActionResult>();
    }
}

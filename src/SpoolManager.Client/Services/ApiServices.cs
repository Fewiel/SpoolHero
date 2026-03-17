using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.DTOs.Tags;
using SpoolManager.Shared.DTOs.SlicerProfiles;
using SpoolManager.Shared.DTOs.Import;

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

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/spools/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/spools/{id}/image");

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

    public Task<HttpResponseMessage> CreateAsync(CreateMaterialRequest request) => _http.PostAsJsonAsync("api/materials", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateMaterialRequest request) => _http.PutAsJsonAsync($"api/materials/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/materials/{id}");

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/materials/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/materials/{id}/image");

    public async Task<string?> ExportAsync(List<Guid>? ids = null)
    {
        var q = ids != null ? $"?ids={string.Join(",", ids)}" : string.Empty;
        var response = await _http.GetFromJsonAsync<ExportResult>($"api/materials/export{q}");
        return response?.Base64;
    }

    public Task<HttpResponseMessage> ImportAsync(string base64) => _http.PostAsJsonAsync("api/materials/import", new { base64 });

    public Task<MaterialSearchResult?> SearchAsync(MaterialSearchRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Query)) parts.Add($"query={Uri.EscapeDataString(request.Query)}");
        if (!string.IsNullOrWhiteSpace(request.MaterialType)) parts.Add($"materialType={Uri.EscapeDataString(request.MaterialType)}");
        if (!string.IsNullOrWhiteSpace(request.Brand)) parts.Add($"brand={Uri.EscapeDataString(request.Brand)}");
        if (request.GlobalOnly.HasValue) parts.Add($"globalOnly={request.GlobalOnly.Value.ToString().ToLower()}");
        parts.Add($"limit={request.Limit}");
        parts.Add($"offset={request.Offset}");
        var q = parts.Any() ? "?" + string.Join("&", parts) : string.Empty;
        return _http.GetFromJsonAsync<MaterialSearchResult>($"api/materials/search{q}");
    }

    public Task<List<string>?> GetBrandsAsync() => _http.GetFromJsonAsync<List<string>>("api/materials/brands");

    public Task<List<string>?> GetTypesAsync() => _http.GetFromJsonAsync<List<string>>("api/materials/types");

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

    public Task<HttpResponseMessage> DownloadAsync(Guid spoolId) =>
        _http.GetAsync($"api/tags/download/{spoolId}");
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

public class SlicerProfileService
{
    private readonly HttpClient _http;
    public SlicerProfileService(HttpClient http) => _http = http;

    public Task<List<SlicerProfileDto>?> GetByMaterialAsync(Guid materialId, Guid? printerId = null)
    {
        var q = printerId.HasValue ? $"?printerId={printerId}" : string.Empty;
        return _http.GetFromJsonAsync<List<SlicerProfileDto>>($"api/materials/{materialId}/slicer-profiles{q}");
    }

    public Task<SlicerProfileDto?> GetByIdAsync(Guid materialId, Guid id) =>
        _http.GetFromJsonAsync<SlicerProfileDto>($"api/materials/{materialId}/slicer-profiles/{id}");

    public Task<HttpResponseMessage> CreateAsync(Guid materialId, CreateSlicerProfileRequest request) =>
        _http.PostAsJsonAsync($"api/materials/{materialId}/slicer-profiles", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid materialId, Guid id, UpdateSlicerProfileRequest request) =>
        _http.PutAsJsonAsync($"api/materials/{materialId}/slicer-profiles/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid materialId, Guid id) =>
        _http.DeleteAsync($"api/materials/{materialId}/slicer-profiles/{id}");

    public string GetExportUrl(Guid materialId, Guid id, string format) =>
        $"api/materials/{materialId}/slicer-profiles/{id}/export?format={format}";

    public Task<HttpResponseMessage> DownloadExportAsync(Guid materialId, Guid id, string format) =>
        _http.GetAsync($"api/materials/{materialId}/slicer-profiles/{id}/export?format={format}");
}

public class CsvImportService
{
    private readonly HttpClient _http;
    public CsvImportService(HttpClient http) => _http = http;

    public async Task<CsvImportResult?> ImportAsync(CsvImportRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/import/csv", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CsvImportResult>();
    }
}

using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Storage;

namespace SpoolManager.Client.Services;

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

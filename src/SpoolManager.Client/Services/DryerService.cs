using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Dryers;

namespace SpoolManager.Client.Services;

public class DryerService
{
    private readonly HttpClient _http;
    public DryerService(HttpClient http) => _http = http;

    public Task<List<DryerDto>?> GetAllAsync() =>
        _http.GetFromJsonAsync<List<DryerDto>>("api/dryers");

    public Task<DryerDto?> GetByIdAsync(Guid id) =>
        _http.GetFromJsonAsync<DryerDto>($"api/dryers/{id}");

    public Task<HttpResponseMessage> CreateAsync(CreateDryerRequest request) =>
        _http.PostAsJsonAsync("api/dryers", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateDryerRequest request) =>
        _http.PutAsJsonAsync($"api/dryers/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _http.DeleteAsync($"api/dryers/{id}");

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/dryers/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/dryers/{id}/image");
}

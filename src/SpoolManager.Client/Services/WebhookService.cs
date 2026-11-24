using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Webhooks;

namespace SpoolManager.Client.Services;

public class WebhookService
{
    private readonly HttpClient _http;
    public WebhookService(HttpClient http) => _http = http;

    public Task<List<WebhookDto>?> GetAllAsync() =>
        _http.GetFromJsonAsync<List<WebhookDto>>("api/webhooks");

    public Task<HttpResponseMessage> CreateAsync(SaveWebhookRequest request) =>
        _http.PostAsJsonAsync("api/webhooks", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, SaveWebhookRequest request) =>
        _http.PutAsJsonAsync($"api/webhooks/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _http.DeleteAsync($"api/webhooks/{id}");

    public async Task<WebhookTestResult?> TestAsync(Guid id)
    {
        var resp = await _http.PostAsync($"api/webhooks/{id}/test", null);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<WebhookTestResult>() : null;
    }
}

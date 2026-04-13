using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Services;

public class InventoryService
{
    private readonly HttpClient _http;
    public InventoryService(HttpClient http) => _http = http;

    public async Task<InventoryIdentifyResult?> IdentifyAsync(InventoryIdentifyRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/inventory/identify", request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            return null;
        return await response.Content.ReadFromJsonAsync<InventoryIdentifyResult>();
    }

    public async Task<InventoryActionResult?> PerformActionAsync(InventoryActionRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/inventory/action", request);
        return await response.Content.ReadFromJsonAsync<InventoryActionResult>();
    }
}

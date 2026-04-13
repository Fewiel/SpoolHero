using System.Net.Http.Json;
using Microsoft.JSInterop;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Services;

public class TagService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public TagService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public Task<TagEncodeResponse?> EncodeAsync(TagEncodeRequest request) =>
        _http.PostAsJsonAsync("api/tags/encode", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagEncodeResponse>() : null).Unwrap();

    public Task<TagDecodeResponse?> DecodeAsync(TagDecodeRequest request) =>
        _http.PostAsJsonAsync("api/tags/decode", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagDecodeResponse>() : null).Unwrap();

    public Task<TagEncodeResponse?> EncodeEntityAsync(TagEncodeEntityRequest request) =>
        _http.PostAsJsonAsync("api/tags/encode-entity", request).ContinueWith(async t =>
            (await t).IsSuccessStatusCode ? await (await t).Content.ReadFromJsonAsync<TagEncodeResponse>() : null).Unwrap();

    public async Task DownloadFileAsync(string url, string filename)
    {
        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await _js.InvokeVoidAsync("downloadHelper.saveAs", bytes, filename);
    }

    public Task DownloadSpoolBinAsync(Guid spoolId) =>
        DownloadFileAsync($"api/tags/download/{spoolId}", $"tag_{spoolId}.bin");

    public Task DownloadSpoolJsonAsync(Guid spoolId) =>
        DownloadFileAsync($"api/tags/download/{spoolId}/json", $"tag_{spoolId}.json");

    public Task DownloadEntityBinAsync(string entityType, Guid entityId) =>
        DownloadFileAsync($"api/tags/download/entity/{entityType}/{entityId}", $"tag_{entityType}_{entityId}.bin");

    public Task DownloadEntityJsonAsync(string entityType, Guid entityId) =>
        DownloadFileAsync($"api/tags/download/entity/{entityType}/{entityId}/json", $"tag_{entityType}_{entityId}.json");
}

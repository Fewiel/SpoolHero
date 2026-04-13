using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Tags;

namespace SpoolManager.Client.Services;

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

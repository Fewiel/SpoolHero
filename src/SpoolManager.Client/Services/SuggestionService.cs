using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Suggestions;

namespace SpoolManager.Client.Services;

public class SuggestionService
{
    private readonly HttpClient _http;
    public SuggestionService(HttpClient http) => _http = http;

    public Task<List<MaterialSuggestionDto>?> GetMyAsync() =>
        _http.GetFromJsonAsync<List<MaterialSuggestionDto>>("api/suggestions");

    public Task<HttpResponseMessage> CreateAsync(CreateSuggestionRequest request) =>
        _http.PostAsJsonAsync("api/suggestions", request);

    public Task<MaterialSuggestionDto?> GetByIdAsync(Guid id) =>
        _http.GetFromJsonAsync<MaterialSuggestionDto>($"api/suggestions/{id}");
}

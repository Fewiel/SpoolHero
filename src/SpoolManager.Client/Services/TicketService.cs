using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Services;

public class TicketService
{
    private readonly HttpClient _http;

    public TicketService(HttpClient http) => _http = http;

    public Task<List<SupportTicketDto>?> GetMyTicketsAsync() =>
        _http.GetFromJsonAsync<List<SupportTicketDto>>("api/tickets");

    public Task<TicketDetailDto?> GetDetailAsync(Guid id) =>
        _http.GetFromJsonAsync<TicketDetailDto>($"api/tickets/{id}");

    public Task<HttpResponseMessage> CreateAsync(CreateTicketRequest request) =>
        _http.PostAsJsonAsync("api/tickets", request);

    public Task<HttpResponseMessage> CloseAsync(Guid id) =>
        _http.PostAsJsonAsync($"api/tickets/{id}/close", new { });

    public Task<HttpResponseMessage> AddCommentAsync(Guid id, string content) =>
        _http.PostAsJsonAsync($"api/tickets/{id}/comments", new { content });

    public async Task TrackReorderClickAsync(Guid materialId)
    {
        try { await _http.PostAsJsonAsync($"api/materials/{materialId}/click", new { }); } catch { }
    }
}

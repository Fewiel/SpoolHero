using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Services;

public class AuditLogService
{
    private readonly HttpClient _http;

    public AuditLogService(HttpClient http) => _http = http;

    public Task<AuditLogPageResponse?> GetAuditLogAsync(int limit = 50, int offset = 0, string? action = null, string? user = null)
    {
        var url = $"api/admin/audit?limit={limit}&offset={offset}";
        if (!string.IsNullOrWhiteSpace(action)) url += $"&action={Uri.EscapeDataString(action)}";
        if (!string.IsNullOrWhiteSpace(user)) url += $"&user={Uri.EscapeDataString(user)}";
        return _http.GetFromJsonAsync<AuditLogPageResponse>(url);
    }
}

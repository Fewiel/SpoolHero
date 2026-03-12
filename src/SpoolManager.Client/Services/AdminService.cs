using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Admin;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Services;

public class AdminService
{
    private readonly HttpClient _http;

    public AdminService(HttpClient http) => _http = http;

    public Task<List<AdminUserDto>?> GetUsersAsync() =>
        _http.GetFromJsonAsync<List<AdminUserDto>>("api/admin/users");

    public Task<HttpResponseMessage> SetAdminAsync(Guid id, bool isAdmin) =>
        _http.PutAsJsonAsync($"api/admin/users/{id}/admin", new { isAdmin });

    public Task<HttpResponseMessage> DeleteUserAsync(Guid id) =>
        _http.DeleteAsync($"api/admin/users/{id}");

    public Task<AdminStatsDto?> GetStatsAsync() =>
        _http.GetFromJsonAsync<AdminStatsDto>("api/admin/stats");

    public Task<List<SupportTicketDto>?> GetTicketsAsync(string? status = null, string? search = null)
    {
        var url = "api/admin/tickets";
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);
        return _http.GetFromJsonAsync<List<SupportTicketDto>>(url);
    }

    public Task<HttpResponseMessage> SetTicketStatusAsync(Guid id, string status) =>
        _http.PutAsJsonAsync($"api/admin/tickets/{id}/status", new { status });

    public Task<HttpResponseMessage> AssignTicketAsync(Guid id, Guid? assignedToUserId) =>
        _http.PutAsJsonAsync($"api/admin/tickets/{id}/assign", new { assignedToUserId });

    public Task<HttpResponseMessage> AddAdminCommentAsync(Guid id, string content, bool isInternal = false) =>
        _http.PostAsJsonAsync($"api/admin/tickets/{id}/comments", new { content, isInternal });

    public Task<SmtpSettingsDto?> GetSmtpSettingsAsync() =>
        _http.GetFromJsonAsync<SmtpSettingsDto>("api/admin/settings/smtp");

    public Task<HttpResponseMessage> SaveSmtpSettingsAsync(SmtpSettingsDto dto) =>
        _http.PutAsJsonAsync("api/admin/settings/smtp", dto);

    public Task<HttpResponseMessage> TestSmtpAsync(string toEmail) =>
        _http.PostAsJsonAsync("api/admin/settings/smtp/test", new { toEmail });

    public Task<LegalSettingsDto?> GetLegalSettingsAsync() =>
        _http.GetFromJsonAsync<LegalSettingsDto>("api/admin/settings/legal");

    public Task<HttpResponseMessage> SaveLegalSettingsAsync(LegalSettingsDto dto) =>
        _http.PutAsJsonAsync("api/admin/settings/legal", dto);

    public Task<BrandingDto?> GetBrandingAsync() =>
        _http.GetFromJsonAsync<BrandingDto>("api/public/branding");

    public Task<HttpResponseMessage> UploadLogoAsync(MultipartFormDataContent content) =>
        _http.PostAsync("api/admin/settings/logo", content);

    public Task<HttpResponseMessage> DeleteLogoAsync() =>
        _http.DeleteAsync("api/admin/settings/logo");

    public Task<HttpResponseMessage> UploadLogoDarkAsync(MultipartFormDataContent content) =>
        _http.PostAsync("api/admin/settings/logo-dark", content);

    public Task<HttpResponseMessage> DeleteLogoDarkAsync() =>
        _http.DeleteAsync("api/admin/settings/logo-dark");

    public Task<HttpResponseMessage> UploadFaviconAsync(MultipartFormDataContent content) =>
        _http.PostAsync("api/admin/settings/favicon", content);

    public Task<HttpResponseMessage> DeleteFaviconAsync() =>
        _http.DeleteAsync("api/admin/settings/favicon");

    public Task<HttpResponseMessage> SetLandingPageEnabledAsync(bool enabled) =>
        _http.PutAsJsonAsync("api/admin/settings/landing-page", new SetLandingPageRequest { Enabled = enabled });
}

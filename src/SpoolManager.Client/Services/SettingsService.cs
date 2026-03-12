using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Admin;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Services;

public class SettingsService
{
    private readonly HttpClient _http;

    public SettingsService(HttpClient http) => _http = http;

    public Task<NotificationPrefsDto?> GetNotificationPrefsAsync() =>
        _http.GetFromJsonAsync<NotificationPrefsDto>("api/auth/notifications");

    public Task<HttpResponseMessage> SaveNotificationPrefsAsync(NotificationPrefsDto dto) =>
        _http.PutAsJsonAsync("api/auth/notifications", dto);

    public Task<HttpResponseMessage> UpdateLanguageAsync(string lang) =>
        _http.PutAsJsonAsync("api/auth/language", new SetLanguageRequest { Language = lang });
}

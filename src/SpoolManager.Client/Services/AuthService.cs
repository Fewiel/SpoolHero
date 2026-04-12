using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public string? Token { get; private set; }
    public UserDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => Token != null && !IsTokenExpired(Token);

    public event Action? OnAuthStateChanged;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_token");
            if (!string.IsNullOrEmpty(token) && !IsTokenExpired(token))
            {
                Token = token;
                SetAuthHeader();
                await LoadCurrentUserAsync();
            }
        }
        catch { }
    }

    public async Task<bool> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result == null) return false;

            Token = result.AccessToken;
            await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", Token);
            SetAuthHeader();
            await LoadCurrentUserAsync();
            OnAuthStateChanged?.Invoke();
            return true;
        }
        catch { return false; }
    }

    public async Task UpdateTokenAsync(string newToken)
    {
        Token = newToken;
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", Token);
        SetAuthHeader();
        await LoadCurrentUserAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task<HttpResponseMessage> DeleteAccountAsync(string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "api/auth/account")
        {
            Content = JsonContent.Create(new { password })
        };
        return await _http.SendAsync(request);
    }

    public async Task<bool> SetDashboardOnboardingDismissedAsync(bool dismissed)
    {
        try
        {
            var response = await _http.PutAsJsonAsync("api/auth/dashboard-onboarding", new SetDashboardOnboardingRequest
            {
                Dismissed = dismissed
            });

            if (!response.IsSuccessStatusCode)
                return false;

            if (CurrentUser != null)
                CurrentUser.DashboardOnboardingDismissed = dismissed;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        Token = null;
        CurrentUser = null;
        _http.DefaultRequestHeaders.Authorization = null;
        try { await _js.InvokeVoidAsync("localStorage.removeItem", "auth_token"); } catch { }
        OnAuthStateChanged?.Invoke();
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            CurrentUser = await _http.GetFromJsonAsync<UserDto>("api/auth/me");
        }
        catch { }
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
    }

    public Task<HttpResponseMessage> ForgotPasswordAsync(string email) =>
        _http.PostAsJsonAsync("api/auth/forgot-password", new { email });

    public Task<HttpResponseMessage> ResetPasswordAsync(string token, string newPassword) =>
        _http.PostAsJsonAsync("api/auth/reset-password", new { token, newPassword });

    public Task<HttpResponseMessage> ResendVerificationAsync(string email) =>
        _http.PostAsJsonAsync("api/auth/resend-verification", new { email });

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) < DateTimeOffset.UtcNow;
            return false;
        }
        catch { return true; }
    }
}

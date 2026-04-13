using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Pages.Auth;

public partial class Register
{
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly RegisterRequest _model = new();
    private bool _loading;
    private bool _success;
    private bool _requiresVerification;
    private string? _error;
    private bool _termsAccepted;
    private bool _privacyAccepted;

    private async Task HandleRegister()
    {
        if (!_termsAccepted || !_privacyAccepted)
        {
            _error = L["auth.terms.required"];
            return;
        }
        _loading = true;
        _error = null;
        _success = false;
        var resp = await Http.PostAsJsonAsync("api/auth/register", _model);
        _loading = false;
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            if (json?.RequiresEmailVerification == true)
                _requiresVerification = true;
            else
            { _success = true; await Task.Delay(1500); Nav.NavigateTo("/login"); }
        }
        else _error = await resp.Content.ReadAsStringAsync();
    }

    private record RegisterResponse(bool RequiresEmailVerification);
}

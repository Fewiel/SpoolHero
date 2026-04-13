using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class ForgotPassword
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string _email = string.Empty;
    private bool _loading;
    private bool _sent;
    private string? _error;

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(_email)) { _error = L["auth.email.required"]; return; }
        _loading = true;
        _error = null;
        var resp = await Auth.ForgotPasswordAsync(_email);
        _loading = false;
        _sent = resp.IsSuccessStatusCode;
        if (!_sent) _error = L["common.error"];
    }
}

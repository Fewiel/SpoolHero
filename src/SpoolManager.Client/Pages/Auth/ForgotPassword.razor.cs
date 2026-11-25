using System.Net;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class ForgotPassword
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private string _email = string.Empty;
    private bool _loading;
    private bool _sent;
    private bool _emailInvalid;
    private string? _error;

    private async Task SendAsync()
    {
        _error = null;
        _email = _email.Trim();
        _emailInvalid = !LoginForm.IsEmail(_email);
        if (_emailInvalid)
            return;

        _loading = true;
        try
        {
            var resp = await Auth.ForgotPasswordAsync(_email);
            _sent = resp.IsSuccessStatusCode;
            if (!_sent)
                _error = resp.StatusCode == HttpStatusCode.TooManyRequests ? L["auth.too.many.requests"] : L["auth.forgot.failed"];
        }
        catch
        {
            _error = L["auth.forgot.failed"];
        }
        _loading = false;
    }
}

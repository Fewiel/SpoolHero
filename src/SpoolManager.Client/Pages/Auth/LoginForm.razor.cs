using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Pages.Auth;

/// <summary>Sign-in form used by the simple login page and the landing page.</summary>
public partial class LoginForm
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    /// <summary>Prefix for element ids, so the form can appear on different pages without id clashes.</summary>
    [Parameter] public string IdPrefix { get; set; } = "login";

    private readonly LoginRequest _model = new();
    private bool _loading;
    private string? _error;
    private LoginResult? _result;
    private bool _emailInvalid;
    private bool _passwordInvalid;

    private async Task HandleLogin()
    {
        var email = _model.Email?.Trim() ?? string.Empty;
        _emailInvalid = !IsEmail(email);
        _passwordInvalid = string.IsNullOrEmpty(_model.Password);
        _error = null;
        _result = null;
        if (_emailInvalid || _passwordInvalid)
            return;

        _model.Email = email;
        _loading = true;
        _result = await Auth.LoginWithResultAsync(_model);
        _loading = false;

        if (_result == LoginResult.Success)
        {
            Nav.NavigateTo("/");
            return;
        }

        _error = _result switch
        {
            LoginResult.EmailNotVerified => L["auth.email.unverified"],
            LoginResult.TooManyAttempts => L["auth.login.blocked"],
            LoginResult.InvalidCredentials => L["auth.login.error"],
            _ => L["auth.login.failed"]
        };
    }

    internal static bool IsEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0 && !value.Contains(' ');
    }
}

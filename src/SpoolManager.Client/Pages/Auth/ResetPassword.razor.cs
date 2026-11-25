using System.Net;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class ResetPassword
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string _token = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _loading;
    private bool _success;
    private bool _linkBroken;
    private bool _linkExpired;
    private string? _error;
    private string? _passwordError;
    private string? _confirmError;

    protected override void OnInitialized()
    {
        var uri = new Uri(Nav.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        _token = query["token"] ?? string.Empty;
    }

    private async Task ResetAsync()
    {
        _error = null;
        _passwordError = _password.Length < 8 ? L["auth.password.min"] : null;
        _confirmError = _passwordError == null && _password != _confirmPassword ? L["auth.password.mismatch"] : null;
        if (_passwordError != null || _confirmError != null)
            return;

        _loading = true;
        try
        {
            var resp = await Auth.ResetPasswordAsync(_token, _password);
            if (resp.IsSuccessStatusCode)
            {
                _success = true;
            }
            else if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _error = L["auth.too.many.requests"];
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                if (body.Contains("Password", StringComparison.Ordinal))
                {
                    _passwordError = L["auth.password.min"];
                }
                else
                {
                    _linkBroken = true;
                    _linkExpired = body.Contains("Token expired", StringComparison.Ordinal);
                }
            }
        }
        catch
        {
            _error = L["common.error.retry"];
        }
        _loading = false;
    }
}

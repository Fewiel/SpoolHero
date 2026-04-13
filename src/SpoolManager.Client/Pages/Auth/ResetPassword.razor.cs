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
    private string? _error;

    protected override void OnInitialized()
    {
        var uri = new Uri(Nav.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        _token = query["token"] ?? string.Empty;
    }

    private async Task ResetAsync()
    {
        if (_password.Length < 8) { _error = L["auth.password.min"]; return; }
        if (_password != _confirmPassword) { _error = L["auth.password.mismatch"]; return; }

        _loading = true;
        _error = null;
        var resp = await Auth.ResetPasswordAsync(_token, _password);
        _loading = false;

        if (resp.IsSuccessStatusCode)
        {
            _success = true;
        }
        else
        {
            var body = await resp.Content.ReadAsStringAsync();
            _error = body.Contains("expired") ? L["auth.reset.expired"] : L["auth.reset.invalid"];
        }
    }
}

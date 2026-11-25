using System.Net;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class VerifyEmail
{
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private bool _success;
    private bool _hadToken;
    private bool _resending;
    private bool _resendSuccess;
    private bool _emailInvalid;
    private string? _resendError;
    private string _resendEmail = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(Nav.Uri);
        var token = System.Web.HttpUtility.ParseQueryString(uri.Query)["token"];
        if (string.IsNullOrEmpty(token))
        {
            _loading = false;
            return;
        }

        _hadToken = true;
        try
        {
            var resp = await Http.GetAsync($"api/auth/verify-email?token={Uri.EscapeDataString(token)}");
            _success = resp.IsSuccessStatusCode;
        }
        catch
        {
            _success = false;
        }
        _loading = false;
    }

    private async Task ResendAsync()
    {
        _resendError = null;
        _resendEmail = _resendEmail.Trim();
        _emailInvalid = !LoginForm.IsEmail(_resendEmail);
        if (_emailInvalid)
            return;

        _resending = true;
        try
        {
            var resp = await Auth.ResendVerificationAsync(_resendEmail);
            _resendSuccess = resp.IsSuccessStatusCode;
            if (!_resendSuccess)
                _resendError = resp.StatusCode == HttpStatusCode.TooManyRequests ? L["auth.too.many.requests"] : L["auth.resend.failed"];
        }
        catch
        {
            _resendError = L["auth.resend.failed"];
        }
        _resending = false;
    }
}

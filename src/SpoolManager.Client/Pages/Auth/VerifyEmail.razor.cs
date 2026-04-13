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
    private bool _resending;
    private bool _resendSuccess;
    private string _resendEmail = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(Nav.Uri);
        var token = System.Web.HttpUtility.ParseQueryString(uri.Query)["token"];
        if (string.IsNullOrEmpty(token)) { _loading = false; return; }

        var resp = await Http.GetAsync($"api/auth/verify-email?token={Uri.EscapeDataString(token)}");
        _success = resp.IsSuccessStatusCode;
        _loading = false;
    }

    private async Task ResendAsync()
    {
        if (string.IsNullOrWhiteSpace(_resendEmail)) return;
        _resending = true;
        var resp = await Auth.ResendVerificationAsync(_resendEmail);
        _resendSuccess = resp.IsSuccessStatusCode;
        _resending = false;
    }
}

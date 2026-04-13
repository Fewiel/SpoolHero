using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Pages.Auth;

public partial class Login
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private BrandingService Branding { get; set; } = default!;

    private readonly LoginRequest _model = new();
    private bool _loading;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.IsAuthenticated) Nav.NavigateTo("/");
        if (string.IsNullOrEmpty(Branding.LogoDataUrl))
            await Branding.InitializeAsync();
    }

    private async Task HandleLogin()
    {
        _loading = true;
        _error = null;
        var ok = await Auth.LoginAsync(_model);
        _loading = false;
        if (ok) Nav.NavigateTo("/");
        else _error = L["auth.login.error"];
    }
}

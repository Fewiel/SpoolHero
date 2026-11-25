using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class Login
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private BrandingService Branding { get; set; } = default!;

    private sealed record PreviewSpool(string Type, string ColorKey, string Hex, int Percent);
    private sealed record Feature(string Key, string Icon);

    private static readonly PreviewSpool[] PreviewSpools =
    [
        new("PLA Matte", "landing.preview.color.black", "1D1F24", 82),
        new("PETG", "landing.preview.color.white", "F4F4F2", 64),
        new("PLA Silk", "landing.preview.color.blue", "2F6BFF", 47),
        new("ASA", "landing.preview.color.orange", "FF6A13", 12),
        new("TPU 95A", "landing.preview.color.red", "D62839", 35),
        new("PLA", "landing.preview.color.green", "2A9D5B", 91),
    ];

    private static readonly Feature[] Features =
    [
        new("landing.feature.rfid", "bi-broadcast"),
        new("landing.feature.materials", "bi-database-check"),
        new("landing.feature.projects", "bi-people"),
        new("landing.feature.dryer", "bi-thermometer-half"),
        new("landing.feature.inventory", "bi-arrow-left-right"),
        new("landing.feature.selfhost", "bi-lightning-charge"),
    ];

    protected override void OnInitialized()
    {
        if (Auth.IsAuthenticated)
            Nav.NavigateTo("/");
    }
}

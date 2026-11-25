using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>Main navigation, shared by the desktop sidebar and the mobile menu.</summary>
public partial class AppNav
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;

    /// <summary>Called when a link is chosen (used to close the mobile menu).</summary>
    [Parameter] public EventCallback OnNavigate { get; set; }

    private bool IsAdmin => Auth.CurrentUser?.IsPlatformAdmin == true;
}

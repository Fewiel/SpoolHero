using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>Shown by the router when no page matches the address.</summary>
public partial class NotFoundView
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
}

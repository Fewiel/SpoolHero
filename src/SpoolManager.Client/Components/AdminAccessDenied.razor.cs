using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>Shown on admin pages when the signed-in user is not a platform admin.</summary>
public partial class AdminAccessDenied
{
    [Inject] private LocalizationService L { get; set; } = default!;
}

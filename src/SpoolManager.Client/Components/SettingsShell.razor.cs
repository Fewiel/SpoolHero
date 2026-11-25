using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>
/// Shared frame for the personal settings pages (account, password, notifications):
/// page header, settings navigation and a content column for <c>.settings-section</c> cards.
/// </summary>
public partial class SettingsShell
{
    [Inject] private LocalizationService L { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}

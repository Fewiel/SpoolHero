using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

/// <summary>Favourite / reorder / suggest / clone / edit / delete buttons of a material in the list.</summary>
public partial class MaterialRowActions
{
    [Inject] private LocalizationService L { get; set; } = default!;

    [Parameter, EditorRequired] public MaterialSummaryDto Material { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool IsFavorite { get; set; }
    [Parameter] public EventCallback OnToggleFavorite { get; set; }
    [Parameter] public EventCallback OnDelete { get; set; }
    [Parameter] public EventCallback OnReorder { get; set; }

    private string BtnSize => Compact ? "btn-sm" : string.Empty;
}

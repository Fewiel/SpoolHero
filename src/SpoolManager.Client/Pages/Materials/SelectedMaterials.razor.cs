using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

/// <summary>Removable list of materials selected for an export.</summary>
public partial class SelectedMaterials
{
    [Inject] private LocalizationService L { get; set; } = default!;

    [Parameter, EditorRequired] public List<FilamentMaterialDto> Items { get; set; } = [];
    [Parameter] public EventCallback<Guid> OnRemove { get; set; }
    [Parameter] public string? EmptyText { get; set; }
    [Parameter] public Func<FilamentMaterialDto, bool>? WarnWhen { get; set; }
    [Parameter] public string? WarnText { get; set; }
}

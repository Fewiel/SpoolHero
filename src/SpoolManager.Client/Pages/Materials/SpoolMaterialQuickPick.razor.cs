using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Materials;

/// <summary>Shows the materials of the project's active spools as toggle buttons.</summary>
public partial class SpoolMaterialQuickPick
{
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    /// <summary>Ids of the materials that are currently selected.</summary>
    [Parameter] public IReadOnlyCollection<Guid> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<Guid> OnAdd { get; set; }
    [Parameter] public EventCallback<Guid> OnRemove { get; set; }

    private bool _loading = true;
    private List<Group> _groups = [];

    private sealed record Group(Guid MaterialId, string Brand, string Type, string ColorHex, string? ColorName, int Count, decimal Percent);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var spools = await Spools.GetAllAsync(consumed: false) ?? [];
            _groups = spools
                .GroupBy(s => s.FilamentMaterialId)
                .Select(g =>
                {
                    var f = g.First();
                    return new Group(g.Key, f.MaterialBrand, f.MaterialType, f.MaterialColorHex, f.MaterialColorName, g.Count(), g.Max(s => s.RemainingPercent));
                })
                .OrderBy(g => g.Brand).ThenBy(g => g.Type).ThenBy(g => g.ColorName)
                .ToList();
        }
        catch (HttpRequestException)
        {
            _groups = [];
        }
        _loading = false;
    }

    private bool IsSelected(Guid id) => SelectedIds.Contains(id);

    private Task Toggle(Guid id) => IsSelected(id) ? OnRemove.InvokeAsync(id) : OnAdd.InvokeAsync(id);

    private async Task AddAllAsync()
    {
        foreach (var g in _groups.Where(g => !IsSelected(g.MaterialId)).ToList())
            await OnAdd.InvokeAsync(g.MaterialId);
    }
}

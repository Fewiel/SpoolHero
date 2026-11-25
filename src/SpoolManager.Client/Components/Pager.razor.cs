using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>Pagination below lists and tables: "1–25 of 120" plus page buttons.</summary>
public partial class Pager
{
    [Inject] private LocalizationService L { get; set; } = default!;

    /// <summary>Zero-based page index.</summary>
    [Parameter] public int Page { get; set; }
    [Parameter] public int PageSize { get; set; } = 25;
    [Parameter] public int TotalItems { get; set; }
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    private int First => TotalItems == 0 ? 0 : Page * PageSize + 1;
    private int Last => Math.Min(TotalItems, (Page + 1) * PageSize);

    private IEnumerable<int> VisiblePages
    {
        get
        {
            var start = Math.Max(0, Page - 2);
            var end = Math.Min(TotalPages - 1, start + 4);
            start = Math.Max(0, end - 4);
            return Enumerable.Range(start, end - start + 1);
        }
    }

    private async Task GoAsync(int page)
    {
        page = Math.Clamp(page, 0, TotalPages - 1);
        if (page == Page)
            return;
        await PageChanged.InvokeAsync(page);
    }
}

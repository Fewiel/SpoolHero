using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialList
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private FavoriteService Favorites { get; set; } = default!;

    private const int PageSize = 50;
    private bool _loading = true;
    private int _page;
    private int _totalCount;
    private MaterialSummaryDto[] _items = [];
    private string _typeFilter = string.Empty;
    private string _brandFilter = string.Empty;
    private string _colorFilter = string.Empty;
    private List<string> _types = [];
    private List<string> _brands = [];
    private List<string> _colors = [];
    private MaterialSummaryDto? _deleteTarget;
    private bool _favoritesOnly;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));

    private IEnumerable<int> VisiblePages
    {
        get
        {
            var start = Math.Max(0, _page - 2);
            var end = Math.Min(TotalPages - 1, start + 4);
            start = Math.Max(0, end - 4);
            return Enumerable.Range(start, end - start + 1);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
            return;
        await Favorites.EnsureCacheAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        StateHasChanged();

        var result = await Materials.GetPagedAsync(
            _page, PageSize,
            string.IsNullOrWhiteSpace(_typeFilter) ? null : _typeFilter,
            string.IsNullOrWhiteSpace(_brandFilter) ? null : _brandFilter,
            string.IsNullOrWhiteSpace(_colorFilter) ? null : _colorFilter);

        _items = result.Items.ToArray();
        _totalCount = result.TotalCount;
        _types = result.Types;
        _brands = result.Brands;
        _colors = result.Colors;

        if (_favoritesOnly)
            _items = _items.Where(m => Favorites.IsFavorite(m.Id)).ToArray();

        _loading = false;
        StateHasChanged();
    }

    private async Task ToggleFavorite(Guid id)
    {
        await Favorites.ToggleAsync(id);
        if (_favoritesOnly)
            _items = _items.Where(m => Favorites.IsFavorite(m.Id)).ToArray();
    }

    private async Task OnFavoritesFilterChanged()
    {
        _favoritesOnly = !_favoritesOnly;
        _page = 0;
        await LoadAsync();
    }

    private async Task OnTypeChanged(ChangeEventArgs e)
    {
        _typeFilter = e.Value?.ToString() ?? string.Empty;
        _page = 0;
        await LoadAsync();
    }

    private async Task OnBrandChanged(ChangeEventArgs e)
    {
        _brandFilter = e.Value?.ToString() ?? string.Empty;
        _page = 0;
        await LoadAsync();
    }

    private async Task OnColorChanged(ChangeEventArgs e)
    {
        _colorFilter = e.Value?.ToString() ?? string.Empty;
        _page = 0;
        await LoadAsync();
    }

    private async Task SetPage(int page)
    {
        _page = Math.Clamp(page, 0, TotalPages - 1);
        await LoadAsync();
    }

    private void ConfirmDelete(MaterialSummaryDto m) => _deleteTarget = m;

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        await Materials.DeleteAsync(_deleteTarget.Id);
        _deleteTarget = null;
        await LoadAsync();
    }
}

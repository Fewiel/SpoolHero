using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Materials;

public partial class MaterialList : IDisposable
{
    [Inject] private MaterialService Materials { get; set; } = default!;
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;
    [Inject] private FavoriteService Favorites { get; set; } = default!;

    private const int PageSize = 50;
    private bool _loading = true;
    private string? _error;
    private int _page;
    private int _totalCount;
    private MaterialSummaryDto[] _items = [];
    private string _scope = string.Empty;
    private string _typeFilter = string.Empty;
    private string _brandFilter = string.Empty;
    private string _colorFilter = string.Empty;
    private List<string> _types = [];
    private List<string> _brands = [];
    private List<string> _colors = [];
    private MaterialSummaryDto? _deleteTarget;
    private string? _deleteError;
    private bool _deleting;
    private bool _favoritesOnly;
    private bool _onSpoolOnly;
    private string _sortCol = "spool";
    private bool _sortAsc;
    private int _loadVersion;
    private CancellationTokenSource? _debounce;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(_typeFilter) || !string.IsNullOrWhiteSpace(_brandFilter)
        || !string.IsNullOrWhiteSpace(_colorFilter) || _favoritesOnly || _onSpoolOnly;

    private string SortKey => $"{_sortCol}:{(_sortAsc ? "asc" : "desc")}";

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
        var version = ++_loadVersion;
        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            var result = await Materials.GetPagedAsync(
                _page, PageSize,
                string.IsNullOrWhiteSpace(_typeFilter) ? null : _typeFilter,
                string.IsNullOrWhiteSpace(_brandFilter) ? null : _brandFilter.Trim(),
                string.IsNullOrWhiteSpace(_colorFilter) ? null : _colorFilter.Trim(),
                _sortCol, _sortAsc,
                string.IsNullOrEmpty(_scope) ? null : _scope,
                _onSpoolOnly, _favoritesOnly);

            // A newer request was started meanwhile (fast typing) – drop this stale result.
            if (version != _loadVersion)
                return;

            _items = result.Items.ToArray();
            _totalCount = result.TotalCount;
            _types = result.Types;
            _brands = result.Brands;
            _colors = result.Colors;
        }
        catch (Exception)
        {
            if (version != _loadVersion)
                return;
            _error = L["material.load.error"];
        }

        _loading = false;
        StateHasChanged();
    }

    private async Task ReloadFromFirstPageAsync()
    {
        _page = 0;
        await LoadAsync();
    }

    private async Task DebouncedReloadAsync()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        await ReloadFromFirstPageAsync();
    }

    private async Task SetScopeAsync(string scope)
    {
        if (_scope == scope)
            return;
        _scope = scope;
        await ReloadFromFirstPageAsync();
    }

    private async Task ToggleFavorite(Guid id)
    {
        await Favorites.ToggleAsync(id);
        if (_favoritesOnly)
            await LoadAsync();
    }

    private void TrackReorder(Guid id) => _ = Tickets.TrackReorderClickAsync(id);

    private async Task OnFavoritesFilterChanged()
    {
        _favoritesOnly = !_favoritesOnly;
        await ReloadFromFirstPageAsync();
    }

    private async Task OnSpoolFilterChanged()
    {
        _onSpoolOnly = !_onSpoolOnly;
        await ReloadFromFirstPageAsync();
    }

    private async Task OnTypeChanged(ChangeEventArgs e)
    {
        _typeFilter = e.Value?.ToString() ?? string.Empty;
        await ReloadFromFirstPageAsync();
    }

    private async Task OnBrandChanged(ChangeEventArgs e)
    {
        _brandFilter = e.Value?.ToString() ?? string.Empty;
        await DebouncedReloadAsync();
    }

    private async Task OnColorChanged(ChangeEventArgs e)
    {
        _colorFilter = e.Value?.ToString() ?? string.Empty;
        await DebouncedReloadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _typeFilter = string.Empty;
        _brandFilter = string.Empty;
        _colorFilter = string.Empty;
        _favoritesOnly = false;
        _onSpoolOnly = false;
        await ReloadFromFirstPageAsync();
    }

    private async Task SetPage(int page)
    {
        _page = Math.Clamp(page, 0, TotalPages - 1);
        await LoadAsync();
    }

    private async Task SetSort(string col)
    {
        if (_sortCol == col)
            _sortAsc = !_sortAsc;
        else
        {
            _sortCol = col;
            _sortAsc = col != "spool";
        }
        await ReloadFromFirstPageAsync();
    }

    private async Task OnSortSelected(ChangeEventArgs e)
    {
        var parts = (e.Value?.ToString() ?? "spool:desc").Split(':');
        _sortCol = parts[0];
        _sortAsc = parts.Length > 1 && parts[1] == "asc";
        await ReloadFromFirstPageAsync();
    }

    private string SortIcon(string col) =>
        _sortCol != col ? "bi-chevron-expand mat-sort-idle" : _sortAsc ? "bi-chevron-up" : "bi-chevron-down";

    private string AriaSort(string col) =>
        _sortCol != col ? "none" : _sortAsc ? "ascending" : "descending";

    private string TempText(MaterialSummaryDto m) =>
        m.MinTempCelsius > 0 || m.MaxTempCelsius > 0
            ? $"{m.MinTempCelsius}–{m.MaxTempCelsius} °C"
            : L["material.temp.unknown"];

    private string SourceLabel(MaterialSummaryDto m) =>
        !m.IsGlobal ? L["material.source.project"]
        : m.OfdVariantId != null ? L["material.source.ofd"]
        : L["material.source.global"];

    private string SourceHint(MaterialSummaryDto m) =>
        m.IsGlobal ? L["material.scope.global.hint"] : L["material.scope.project.hint"];

    private void ConfirmDelete(MaterialSummaryDto m)
    {
        _deleteError = null;
        _deleteTarget = m;
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        _deleting = true;
        _deleteError = null;
        try
        {
            var resp = await Materials.DeleteAsync(_deleteTarget.Id);
            if (!resp.IsSuccessStatusCode)
            {
                _deleteError = L["material.delete.failed"];
                return;
            }
            _deleteTarget = null;
            await LoadAsync();
        }
        catch (Exception)
        {
            _deleteError = L["material.delete.failed"];
        }
        finally
        {
            _deleting = false;
        }
    }

    public void Dispose()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
    }
}

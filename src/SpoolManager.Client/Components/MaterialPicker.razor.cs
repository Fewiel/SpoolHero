using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Components;

public partial class MaterialPicker : IDisposable
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private MaterialService MaterialSvc { get; set; } = default!;
    [Inject] private FavoriteService Favorites { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    [Parameter] public Guid? Value { get; set; }
    [Parameter] public EventCallback<Guid?> ValueChanged { get; set; }
    [Parameter] public EventCallback<FilamentMaterialDto?> OnSelected { get; set; }

    private bool _open;
    private bool _searching;
    private string _query = string.Empty;
    private List<FilamentMaterialDto> _results = [];
    private List<FilamentMaterialDto> _favorites = [];
    private FilamentMaterialDto? _selectedMaterial;
    private ElementReference _searchInput;
    private ElementReference _root;
    private DotNetObjectReference<MaterialPicker>? _selfRef;
    private System.Timers.Timer? _debounce;

    protected override async Task OnParametersSetAsync()
    {
        if (Value.HasValue && (_selectedMaterial == null || _selectedMaterial.Id != Value.Value))
            _selectedMaterial = await MaterialSvc.GetByIdAsync(Value.Value);
        else if (!Value.HasValue)
            _selectedMaterial = null;
    }

    private async Task Open()
    {
        _open = true;
        _query = string.Empty;
        _results = [];
        _favorites = [];
        await Task.Yield();
        try { await _searchInput.FocusAsync(); } catch { }
        _selfRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("materialPickerOutside.register", _root, _selfRef);
        _ = LoadFavoritesAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        await Favorites.EnsureCacheAsync();
        var favIds = await Favorites.GetFavoriteIdsAsync();
        if (favIds.Count == 0)
            return;

        var ids = string.Join(",", favIds);
        var materials = await Http.GetFromJsonAsync<List<FilamentMaterialDto>>($"api/materials?ids={Uri.EscapeDataString(ids)}");
        _favorites = materials ?? [];
        StateHasChanged();
    }

    [JSInvokable]
    public void CloseDropdown()
    {
        _open = false;
        _debounce?.Stop();
        StateHasChanged();
    }

    private void OnQuery(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;
        _debounce?.Stop();
        if (string.IsNullOrWhiteSpace(_query))
        {
            _results = [];
            return;
        }
        _debounce = new System.Timers.Timer(300);
        _debounce.Elapsed += async (_, _) =>
        {
            _debounce?.Stop();
            await InvokeAsync(DoSearch);
        };
        _debounce.Start();
    }

    private async Task DoSearch()
    {
        if (string.IsNullOrWhiteSpace(_query))
            return;
        _searching = true;
        StateHasChanged();
        _results = await Http.GetFromJsonAsync<List<FilamentMaterialDto>>(
            $"api/materials/search?q={Uri.EscapeDataString(_query)}&limit=200") ?? [];
        _searching = false;
        StateHasChanged();
    }

    private async Task Select(FilamentMaterialDto m)
    {
        _selectedMaterial = m;
        _open = false;
        _query = string.Empty;
        await JS.InvokeVoidAsync("materialPickerOutside.unregister");
        await ValueChanged.InvokeAsync(m.Id);
        await OnSelected.InvokeAsync(m);
    }

    public void Dispose()
    {
        _debounce?.Dispose();
        _selfRef?.Dispose();
    }
}

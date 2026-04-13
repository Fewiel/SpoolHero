using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminMaterials : IDisposable
{
    [Inject] private AdminMaterialService AdminMat { get; set; } = default!;
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private const int PageSize = 50;
    private bool _loading = true;
    private bool _syncing;
    private int _page;
    private OfdSyncResultDto? _syncResult;
    private List<FilamentMaterialDto> _allMaterials = [];
    private List<FilamentMaterialDto> _filtered = [];
    private string _search = string.Empty;
    private FilamentMaterialDto? _deleteTarget;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
    private List<FilamentMaterialDto> PagedItems => _filtered.Skip(_page * PageSize).Take(PageSize).ToList();

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

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task SyncOfdAsync()
    {
        _syncing = true;
        StateHasChanged();
        var response = await Admin.SyncOfdAsync();
        if (response.IsSuccessStatusCode)
        {
            _syncResult = await response.Content.ReadFromJsonAsync<OfdSyncResultDto>();
            await LoadAsync();
        }
        _syncing = false;
    }

    private record OfdSyncResultDto(int Created, int Updated, int Skipped);

    private async Task LoadAsync()
    {
        _loading = true;
        _allMaterials = await AdminMat.GetAllAsync() ?? [];
        ApplyFilter();
        _loading = false;
    }

    private void ApplyFilter()
    {
        _page = 0;
        if (string.IsNullOrWhiteSpace(_search))
        {
            _filtered = _allMaterials;
            return;
        }
        var q = _search.ToLowerInvariant();
        _filtered = _allMaterials.Where(m =>
            m.Type.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            m.Brand.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (m.ColorName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();
    }

    private void OnSearch(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        ApplyFilter();
    }

    private void SetPage(int page)
    {
        _page = Math.Clamp(page, 0, TotalPages - 1);
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        await AdminMat.DeleteAsync(_deleteTarget.Id);
        _allMaterials.Remove(_deleteTarget);
        _deleteTarget = null;
        ApplyFilter();
    }

    public void Dispose() { }
}

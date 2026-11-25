using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminMaterials
{
    [Inject] private AdminMaterialService AdminMat { get; set; } = default!;
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private const int PageSize = 50;
    private bool _loading = true;
    private bool _loadFailed;
    private bool _syncing;
    private bool _deleting;
    private int _page;
    private string? _error;
    private OfdSyncResultDto? _syncResult;
    private List<FilamentMaterialDto> _allMaterials = [];
    private List<FilamentMaterialDto> _filtered = [];
    private List<string> _types = [];
    private string _search = string.Empty;
    private string _type = string.Empty;
    private FilamentMaterialDto? _deleteTarget;

    private IEnumerable<FilamentMaterialDto> PagedItems => _filtered.Skip(_page * PageSize).Take(PageSize);

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task SyncOfdAsync()
    {
        _syncing = true;
        _error = null;
        _syncResult = null;
        try
        {
            var response = await Admin.SyncOfdAsync();
            if (response.IsSuccessStatusCode)
            {
                _syncResult = await response.Content.ReadFromJsonAsync<OfdSyncResultDto>();
                await LoadAsync();
            }
            else
            {
                _error = L["admin.ofd.sync.failed"];
            }
        }
        catch
        {
            _error = L["admin.ofd.sync.failed"];
        }
        _syncing = false;
    }

    private record OfdSyncResultDto(int Created, int Updated, int Skipped);

    private async Task LoadAsync()
    {
        _loading = true;
        _loadFailed = false;
        try
        {
            _allMaterials = (await AdminMat.GetAllAsync() ?? [])
                .OrderBy(m => m.Brand, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.ColorName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            _loadFailed = true;
            _allMaterials = [];
        }
        _types = _allMaterials.Select(m => m.Type).Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        ApplyFilter(keepPage: true);
        _loading = false;
    }

    private void ApplyFilter(bool keepPage = false)
    {
        IEnumerable<FilamentMaterialDto> q = _allMaterials;
        if (!string.IsNullOrEmpty(_type))
            q = q.Where(m => string.Equals(m.Type, _type, StringComparison.OrdinalIgnoreCase));
        var s = _search.Trim();
        if (s.Length > 0)
        {
            q = q.Where(m =>
                m.Type.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                m.Brand.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                m.ColorHex.Contains(s.TrimStart('#'), StringComparison.OrdinalIgnoreCase) ||
                (m.ColorName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        _filtered = q.ToList();
        var maxPage = Math.Max(0, (int)Math.Ceiling(_filtered.Count / (double)PageSize) - 1);
        _page = keepPage ? Math.Min(_page, maxPage) : 0;
    }

    private void OnSearch(string value)
    {
        _search = value;
        ApplyFilter();
    }

    private void OnTypeChanged(ChangeEventArgs e)
    {
        _type = e.Value?.ToString() ?? string.Empty;
        ApplyFilter();
    }

    private void ResetFilters()
    {
        _search = string.Empty;
        _type = string.Empty;
        ApplyFilter();
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        _deleting = true;
        try
        {
            var resp = await AdminMat.DeleteAsync(_deleteTarget.Id);
            if (resp.IsSuccessStatusCode)
            {
                _allMaterials.Remove(_deleteTarget);
                ApplyFilter(keepPage: true);
            }
            else
            {
                _error = L["admin.materials.delete.failed"];
            }
        }
        catch
        {
            _error = L["admin.materials.delete.failed"];
        }
        _deleting = false;
        _deleteTarget = null;
    }
}

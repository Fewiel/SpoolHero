using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminAuditLog : IDisposable
{
    [Inject] private AuditLogService AuditSvc { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private const int PageSize = 50;

    private bool _loading = true;
    private bool _loadFailed;
    private List<AuditLogDto> _logs = [];
    private int _total;
    private int _offset;
    private string _actionFilter = string.Empty;
    private string _userFilter = string.Empty;
    private System.Timers.Timer? _debounce;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loadFailed = false;
        StateHasChanged();
        try
        {
            var resp = await AuditSvc.GetAuditLogAsync(PageSize, _offset,
                string.IsNullOrWhiteSpace(_actionFilter) ? null : _actionFilter,
                string.IsNullOrWhiteSpace(_userFilter) ? null : _userFilter.Trim());
            _logs = resp?.Logs ?? [];
            _total = resp?.Total ?? 0;
        }
        catch
        {
            _loadFailed = true;
            _logs = [];
            _total = 0;
        }
        _loading = false;
    }

    private void OnUserFilter(string value)
    {
        _userFilter = value;
        _offset = 0;
        _debounce?.Stop();
        _debounce?.Dispose();
        _debounce = new System.Timers.Timer(400) { AutoReset = false };
        _debounce.Elapsed += async (_, _) => await InvokeAsync(async () => { await LoadAsync(); StateHasChanged(); });
        _debounce.Start();
    }

    private async Task OnActionChanged(ChangeEventArgs e)
    {
        _actionFilter = e.Value?.ToString() ?? string.Empty;
        _offset = 0;
        await LoadAsync();
    }

    private async Task ResetFiltersAsync()
    {
        _actionFilter = string.Empty;
        _userFilter = string.Empty;
        _offset = 0;
        await LoadAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        _offset = Math.Max(0, page * PageSize);
        await LoadAsync();
    }

    private static string ActionChipClass(string action) => action switch
    {
        var a when a.Contains("fail") || a.Contains("delete") || a.Contains("blocked") => "sh-chip-danger",
        var a when a.StartsWith("auth.") => "sh-chip-accent",
        var a when a.Contains("admin.") => "sh-chip-warn",
        var a when a.Contains("create") => "sh-chip-ok",
        _ => string.Empty
    };

    public void Dispose() => _debounce?.Dispose();
}

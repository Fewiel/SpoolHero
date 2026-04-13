using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminAuditLog : IDisposable
{
    [Inject] private AuditLogService AuditSvc { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private List<AuditLogDto> _logs = [];
    private int _total;
    private int _offset;
    private const int PageSize = 50;
    private string _actionFilter = string.Empty;
    private string _userFilter = string.Empty;
    private System.Timers.Timer? _debounce;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        StateHasChanged();
        var resp = await AuditSvc.GetAuditLogAsync(PageSize, _offset,
            string.IsNullOrWhiteSpace(_actionFilter) ? null : _actionFilter,
            string.IsNullOrWhiteSpace(_userFilter) ? null : _userFilter);
        _logs = resp?.Logs ?? [];
        _total = resp?.Total ?? 0;
        _loading = false;
    }

    private void OnUserFilter(ChangeEventArgs e)
    {
        _userFilter = e.Value?.ToString() ?? string.Empty;
        _offset = 0;
        _debounce?.Stop();
        _debounce = new System.Timers.Timer(400);
        _debounce.Elapsed += async (_, _) => { _debounce?.Stop(); await InvokeAsync(LoadAsync); };
        _debounce.Start();
    }

    private async Task PrevPage()
    {
        _offset = Math.Max(0, _offset - PageSize);
        await LoadAsync();
    }

    private async Task NextPage()
    {
        _offset += PageSize;
        await LoadAsync();
    }

    private static string GetActionBadgeClass(string action) => action switch
    {
        var a when a.Contains("fail") || a.Contains("delete") => "bg-danger",
        var a when a.StartsWith("auth.") => "bg-primary",
        var a when a.Contains("admin.") => "bg-warning text-dark",
        var a when a.Contains("project.") => "bg-info text-dark",
        _ => "bg-secondary"
    };

    public void Dispose() => _debounce?.Dispose();
}

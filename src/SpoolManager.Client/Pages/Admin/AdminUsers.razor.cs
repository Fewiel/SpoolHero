using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminUsers : IDisposable
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private const int PageSize = 25;

    private bool _loading = true;
    private bool _loadFailed;
    private bool _busy;
    private List<AdminUserDto> _users = [];
    private List<AdminUserDto> _filtered = [];
    private string _search = string.Empty;
    private string _filter = "all";
    private int _page;
    private AdminUserDto? _toggleTarget;
    private AdminUserDto? _deleteTarget;
    private Guid? _resettingOnboardingUserId;
    private string? _message;
    private bool _messageOk;
    private CancellationTokenSource? _hideCts;

    private IEnumerable<AdminUserDto> PagedUsers => _filtered.Skip(_page * PageSize).Take(PageSize);

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
        try
        {
            _users = (await Admin.GetUsersAsync() ?? [])
                .OrderByDescending(u => u.IsSuperAdmin)
                .ThenByDescending(u => u.IsPlatformAdmin)
                .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            _loadFailed = true;
            _users = [];
        }
        ApplyFilter(keepPage: true);
        _loading = false;
    }

    private void ApplyFilter(bool keepPage = false)
    {
        IEnumerable<AdminUserDto> q = _users;
        q = _filter switch
        {
            "admins" => q.Where(u => u.IsPlatformAdmin),
            "unverified" => q.Where(u => !u.EmailVerified),
            _ => q
        };
        var s = _search.Trim();
        if (s.Length > 0)
            q = q.Where(u => u.Username.Contains(s, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(s, StringComparison.OrdinalIgnoreCase));
        _filtered = q.ToList();
        var maxPage = Math.Max(0, (int)Math.Ceiling(_filtered.Count / (double)PageSize) - 1);
        _page = keepPage ? Math.Min(_page, maxPage) : 0;
    }

    private void OnSearch(string value)
    {
        _search = value;
        ApplyFilter();
    }

    private void SetFilter(string filter)
    {
        _filter = filter;
        ApplyFilter();
    }

    private void ResetFilters()
    {
        _filter = "all";
        _search = string.Empty;
        ApplyFilter();
    }

    private static string Initials(string name)
    {
        var parts = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}";
        return name.Length == 0 ? "?" : name[..Math.Min(2, name.Length)];
    }

    private string LastActive(AdminUserDto u) =>
        u.LastActiveAt.HasValue ? u.LastActiveAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : L["admin.users.never"];

    private async Task ConfirmToggleAsync()
    {
        if (_toggleTarget == null)
            return;
        var target = _toggleTarget;
        _busy = true;
        try
        {
            var resp = await Admin.SetAdminAsync(target.Id, !target.IsPlatformAdmin);
            ShowMessage(resp.IsSuccessStatusCode,
                resp.IsSuccessStatusCode
                    ? L.Format(target.IsPlatformAdmin ? "admin.users.demoted" : "admin.users.promoted", target.Username)
                    : L["admin.users.action.failed"]);
        }
        catch
        {
            ShowMessage(false, L["admin.users.action.failed"]);
        }
        _busy = false;
        _toggleTarget = null;
        await LoadAsync();
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        var target = _deleteTarget;
        _busy = true;
        try
        {
            var resp = await Admin.DeleteUserAsync(target.Id);
            ShowMessage(resp.IsSuccessStatusCode,
                resp.IsSuccessStatusCode ? L.Format("admin.users.deleted", target.Username) : L["admin.users.action.failed"]);
        }
        catch
        {
            ShowMessage(false, L["admin.users.action.failed"]);
        }
        _busy = false;
        _deleteTarget = null;
        await LoadAsync();
    }

    private async Task ResetOnboardingAsync(AdminUserDto user)
    {
        _resettingOnboardingUserId = user.Id;
        try
        {
            var response = await Admin.ResetDashboardOnboardingAsync(user.Id);
            ShowMessage(response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? L.Format("admin.users.reset.onboarding.success", user.Username) : L["admin.users.action.failed"]);
        }
        catch
        {
            ShowMessage(false, L["admin.users.action.failed"]);
        }
        _resettingOnboardingUserId = null;
    }

    private void ShowMessage(bool ok, string text)
    {
        _messageOk = ok;
        _message = text;
        _hideCts?.Cancel();
        if (!ok)
            return;
        _hideCts = new CancellationTokenSource();
        _ = AutoHideAsync(_hideCts.Token);
    }

    private async Task AutoHideAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(5000, token);
            _message = null;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
    }
}

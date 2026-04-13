using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminUsers
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private List<AdminUserDto> _users = [];
    private AdminUserDto? _toggleTarget;
    private AdminUserDto? _deleteTarget;
    private Guid? _resettingOnboardingUserId;
    private string? _resetStatusMessage;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _users = await Admin.GetUsersAsync() ?? [];
        _loading = false;
    }

    private void RequestToggle(AdminUserDto user) => _toggleTarget = user;
    private void RequestDelete(AdminUserDto user) => _deleteTarget = user;

    private async Task ConfirmToggleAsync()
    {
        if (_toggleTarget == null)
            return;
        var target = _toggleTarget;
        _toggleTarget = null;
        await Admin.SetAdminAsync(target.Id, !target.IsPlatformAdmin);
        await LoadAsync();
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        var target = _deleteTarget;
        _deleteTarget = null;
        await Admin.DeleteUserAsync(target.Id);
        await LoadAsync();
    }

    private async Task ResetOnboardingAsync(AdminUserDto user)
    {
        _resetStatusMessage = null;
        _resettingOnboardingUserId = user.Id;
        var response = await Admin.ResetDashboardOnboardingAsync(user.Id);
        _resettingOnboardingUserId = null;
        if (response.IsSuccessStatusCode)
        {
            _resetStatusMessage = L["admin.users.reset.onboarding.success"].Replace("{0}", user.Username);
            _ = AutoHideResetStatusAsync();
        }
    }

    private async Task AutoHideResetStatusAsync()
    {
        await Task.Delay(3000);
        _resetStatusMessage = null;
        await InvokeAsync(StateHasChanged);
    }
}

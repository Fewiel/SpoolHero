using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Settings;

public partial class AccountSettings
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _showDeleteModal;
    private string _deletePassword = string.Empty;
    private string? _deleteModalError;
    private bool _deleting;
    private bool _onboardingLoading;
    private bool _onboardingSuccess;
    private string? _onboardingMessage;

    private string Initials
    {
        get
        {
            var name = Auth.CurrentUser?.Username ?? "?";
            var parts = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name[..Math.Min(2, name.Length)];
        }
    }

    private void OpenDelete()
    {
        _deletePassword = string.Empty;
        _deleteModalError = null;
        _showDeleteModal = true;
    }

    private void CloseDelete()
    {
        if (_deleting)
            return;
        _showDeleteModal = false;
        _deletePassword = string.Empty;
        _deleteModalError = null;
    }

    private async Task ShowOnboardingAgainAsync()
    {
        _onboardingLoading = true;
        _onboardingMessage = null;

        var ok = await Auth.SetDashboardOnboardingDismissedAsync(false);
        _onboardingLoading = false;
        _onboardingSuccess = ok;
        _onboardingMessage = ok
            ? L["settings.account.onboarding.success"]
            : L["settings.account.onboarding.error"];
    }

    private async Task DeleteAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(_deletePassword)) return;
        _deleting = true;
        _deleteModalError = null;

        var resp = await Auth.DeleteAccountAsync(_deletePassword);
        _deleting = false;

        if (resp.IsSuccessStatusCode)
        {
            _showDeleteModal = false;
            await Auth.LogoutAsync();
            Nav.NavigateTo("/login");
        }
        else
        {
            _deleteModalError = L["settings.account.delete.error"];
        }
    }
}

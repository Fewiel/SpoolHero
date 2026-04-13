using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Settings;

public partial class NotificationSettings
{
    [Inject] private SettingsService Settings { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private NotificationPrefsDto? _prefs;
    private bool _saving;
    private bool _saved;

    protected override async Task OnInitializedAsync()
    {
        _prefs = await Settings.GetNotificationPrefsAsync();
        _loading = false;
    }

    private async Task SaveAsync()
    {
        if (_prefs == null)
            return;
        _saving = true;
        _saved = false;
        await Settings.SaveNotificationPrefsAsync(_prefs);
        _saving = false;
        _saved = true;
    }
}

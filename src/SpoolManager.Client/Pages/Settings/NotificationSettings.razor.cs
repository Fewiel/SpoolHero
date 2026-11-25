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
    private bool _saveError;
    private bool _dirty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _prefs = await Settings.GetNotificationPrefsAsync();
        }
        catch (HttpRequestException)
        {
            _prefs = null;
        }
        _loading = false;
    }

    private void MarkDirty()
    {
        _dirty = true;
        _saved = false;
        _saveError = false;
    }

    private async Task SaveAsync()
    {
        if (_prefs == null)
            return;
        _saving = true;
        _saved = false;
        _saveError = false;
        try
        {
            var resp = await Settings.SaveNotificationPrefsAsync(_prefs);
            _saved = resp.IsSuccessStatusCode;
            _saveError = !resp.IsSuccessStatusCode;
            if (_saved)
                _dirty = false;
        }
        catch (HttpRequestException)
        {
            _saveError = true;
        }
        _saving = false;
    }
}

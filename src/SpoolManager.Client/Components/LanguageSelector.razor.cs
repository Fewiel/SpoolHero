using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

public partial class LanguageSelector
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private SettingsService Settings { get; set; } = default!;

    private async Task SetLang(string lang)
    {
        await L.SetManualLanguageAsync(lang);
        if (Auth.IsAuthenticated)
            _ = Settings.UpdateLanguageAsync(lang);
        StateHasChanged();
    }
}

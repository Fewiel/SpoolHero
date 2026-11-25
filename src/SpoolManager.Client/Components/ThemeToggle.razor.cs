using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

public partial class ThemeToggle
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private string _pref = "auto";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _pref = await JS.InvokeAsync<string>("themeHelper.getPreference");
        StateHasChanged();
    }

    private async Task SetAsync(string pref)
    {
        _pref = pref;
        await JS.InvokeVoidAsync("themeHelper.setTheme", pref);
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

public partial class ThemeToggle
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _isDark;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var theme = await JS.InvokeAsync<string>("themeHelper.getTheme");
            _isDark = theme == "dark";
            StateHasChanged();
        }
    }

    private async Task ToggleAsync()
    {
        _isDark = !_isDark;
        await JS.InvokeVoidAsync("themeHelper.setTheme", _isDark ? "dark" : "light");
    }
}

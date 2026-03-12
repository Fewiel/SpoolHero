using System.Net.Http.Json;
using Microsoft.JSInterop;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Services;

public class BrandingService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public string? LogoDataUrl { get; private set; }
    public string? LogoDarkDataUrl { get; private set; }
    public string? FaviconDataUrl { get; private set; }
    public bool LandingPageEnabled { get; private set; } = true;

    public event Action? OnBrandingChanged;

    public BrandingService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var dto = await _http.GetFromJsonAsync<BrandingDto>("api/public/branding");
            LogoDataUrl = dto?.LogoDataUrl;
            LogoDarkDataUrl = dto?.LogoDarkDataUrl;
            FaviconDataUrl = dto?.FaviconDataUrl;
            LandingPageEnabled = dto?.LandingPageEnabled ?? true;
            if (!string.IsNullOrEmpty(FaviconDataUrl))
                await ApplyFaviconAsync(FaviconDataUrl);
        }
        catch { }
    }

    public async Task RefreshAsync()
    {
        await InitializeAsync();
        OnBrandingChanged?.Invoke();
    }

    private async Task ApplyFaviconAsync(string dataUrl)
    {
        try
        {
            await _js.InvokeVoidAsync("spoolHeroSetFavicon", dataUrl);
        }
        catch { }
    }
}

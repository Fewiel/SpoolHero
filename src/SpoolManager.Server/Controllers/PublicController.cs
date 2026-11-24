using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly ISiteSettingsRepository _settings;
    private readonly IImageService _images;

    public PublicController(ISiteSettingsRepository settings, IImageService images)
    {
        _settings = settings;
        _images = images;
    }

    [HttpGet("legal")]
    public async Task<IActionResult> GetLegal()
    {
        return Ok(new LegalSettingsDto
        {
            PrivacyDe = await _settings.GetAsync("privacy_de") ?? string.Empty,
            PrivacyEn = await _settings.GetAsync("privacy_en") ?? string.Empty,
            ImprintDe = await _settings.GetAsync("imprint_de") ?? string.Empty,
            ImprintEn = await _settings.GetAsync("imprint_en") ?? string.Empty,
            TermsDe = await _settings.GetAsync("terms_de") ?? string.Empty,
            TermsEn = await _settings.GetAsync("terms_en") ?? string.Empty,
        });
    }

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding()
    {
        var landingEnabled = await _settings.GetAsync("landing_page_enabled");
        return Ok(new BrandingDto
        {
            LogoDataUrl = await _settings.GetAsync("branding_logo"),
            LogoDarkDataUrl = await _settings.GetAsync("branding_logo_dark"),
            FaviconDataUrl = await _settings.GetAsync("branding_favicon"),
            LandingPageEnabled = landingEnabled != "false",
        });
    }

    [HttpGet("og-image")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetOgImage()
    {
        byte[]? logo = null;
        var logoDataUrl = await _settings.GetAsync("branding_logo");
        var commaIdx = logoDataUrl?.IndexOf(',') ?? -1;
        if (commaIdx >= 0)
        {
            try { logo = Convert.FromBase64String(logoDataUrl![(commaIdx + 1)..]); }
            catch (FormatException) { }
        }

        byte[]? icon = null;
        var iconPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "icon-192.png");
        if (logo == null && System.IO.File.Exists(iconPath))
            icon = await System.IO.File.ReadAllBytesAsync(iconPath);

        return File(_images.RenderOgImage(logo, icon), "image/png");
    }

    [HttpGet("favicon")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetFavicon()
    {
        var dataUrl = await _settings.GetAsync("branding_favicon");
        if (string.IsNullOrEmpty(dataUrl))
            return Redirect("/favicon.ico");

        var commaIdx = dataUrl.IndexOf(',');
        if (commaIdx < 0)
            return Redirect("/favicon.ico");

        var mimeStart = dataUrl.IndexOf(':') + 1;
        var mimeEnd = dataUrl.IndexOf(';');
        if (mimeStart <= 0 || mimeEnd <= mimeStart)
            return Redirect("/favicon.ico");

        var mime = dataUrl[mimeStart..mimeEnd];
        var bytes = Convert.FromBase64String(dataUrl[(commaIdx + 1)..]);
        return File(bytes, mime);
    }
}

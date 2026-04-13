using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly ISiteSettingsRepository _settings;

    public PublicController(ISiteSettingsRepository settings) => _settings = settings;

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
        const int width = 1200;
        const int height = 630;
        var bgColor = new Rgba32(15, 23, 42);

        using var canvas = new Image<Rgba32>(width, height, bgColor);

        var logoDataUrl = await _settings.GetAsync("branding_logo");
        if (!string.IsNullOrEmpty(logoDataUrl))
        {
            var commaIdx = logoDataUrl.IndexOf(',');
            if (commaIdx >= 0)
            {
                try
                {
                    var bytes = Convert.FromBase64String(logoDataUrl[(commaIdx + 1)..]);
                    using var logoStream = new MemoryStream(bytes);
                    using var logo = await Image.LoadAsync(logoStream);
                    var scale = Math.Min(800f / logo.Width, 400f / logo.Height);
                    var newW = (int)(logo.Width * scale);
                    var newH = (int)(logo.Height * scale);
                    logo.Mutate(x => x.Resize(newW, newH));
                    canvas.Mutate(ctx => ctx.DrawImage(logo, new Point((width - newW) / 2, (height - newH) / 2), 1f));
                }
                catch { }
            }
        }
        else
        {
            var iconPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "icon-192.png");
            if (System.IO.File.Exists(iconPath))
            {
                using var icon = await Image.LoadAsync(iconPath);
                icon.Mutate(x => x.Resize(300, 300));
                canvas.Mutate(ctx => ctx.DrawImage(icon, new Point(450, 165), 1f));
            }
        }

        using var ms = new MemoryStream();
        await canvas.SaveAsPngAsync(ms);
        return File(ms.ToArray(), "image/png");
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

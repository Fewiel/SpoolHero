using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminSettings
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private BrandingService Branding { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _landingEnabled = true;

    private async Task SaveLandingPageAsync()
    {
        await Admin.SetLandingPageEnabledAsync(_landingEnabled);
        await Branding.RefreshAsync();
    }

    private bool _brandingSaving;
    private bool? _brandingSuccess;
    private string? _brandingMessage;

    private async Task OnLogoSelected(InputFileChangeEventArgs e)
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        try
        {
            var file = e.File;
            using var ms = new MemoryStream();
            await file.OpenReadStream(4_194_304).CopyToAsync(ms);
            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(ms.ToArray()), "file", file.Name);
            content.Headers.ContentType = null;
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content = new MultipartFormDataContent();
            content.Add(fileContent, "file", file.Name);
            var resp = await Admin.UploadLogoAsync(content);
            _brandingSuccess = resp.IsSuccessStatusCode;
            _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
            if (resp.IsSuccessStatusCode)
                await Branding.RefreshAsync();
        }
        catch { _brandingSuccess = false; _brandingMessage = L["common.error"]; }
        _brandingSaving = false;
    }

    private async Task DeleteLogoAsync()
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        var resp = await Admin.DeleteLogoAsync();
        _brandingSuccess = resp.IsSuccessStatusCode;
        _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
        if (resp.IsSuccessStatusCode)
            await Branding.RefreshAsync();
        _brandingSaving = false;
    }

    private async Task OnFaviconSelected(InputFileChangeEventArgs e)
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        try
        {
            var file = e.File;
            using var ms = new MemoryStream();
            await file.OpenReadStream(524_288).CopyToAsync(ms);
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            var content = new MultipartFormDataContent();
            content.Add(fileContent, "file", file.Name);
            var resp = await Admin.UploadFaviconAsync(content);
            _brandingSuccess = resp.IsSuccessStatusCode;
            _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
            if (resp.IsSuccessStatusCode)
                await Branding.RefreshAsync();
        }
        catch { _brandingSuccess = false; _brandingMessage = L["common.error"]; }
        _brandingSaving = false;
    }

    private async Task DeleteFaviconAsync()
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        var resp = await Admin.DeleteFaviconAsync();
        _brandingSuccess = resp.IsSuccessStatusCode;
        _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
        if (resp.IsSuccessStatusCode)
            await Branding.RefreshAsync();
        _brandingSaving = false;
    }

    private async Task OnLogoDarkSelected(InputFileChangeEventArgs e)
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        try
        {
            var file = e.File;
            using var ms = new MemoryStream();
            await file.OpenReadStream(4_194_304).CopyToAsync(ms);
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            var content = new MultipartFormDataContent();
            content.Add(fileContent, "file", file.Name);
            var resp = await Admin.UploadLogoDarkAsync(content);
            _brandingSuccess = resp.IsSuccessStatusCode;
            _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
            if (resp.IsSuccessStatusCode)
                await Branding.RefreshAsync();
        }
        catch { _brandingSuccess = false; _brandingMessage = L["common.error"]; }
        _brandingSaving = false;
    }

    private async Task DeleteLogoDarkAsync()
    {
        _brandingSuccess = null;
        _brandingSaving = true;
        var resp = await Admin.DeleteLogoDarkAsync();
        _brandingSuccess = resp.IsSuccessStatusCode;
        _brandingMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
        if (resp.IsSuccessStatusCode)
            await Branding.RefreshAsync();
        _brandingSaving = false;
    }

    private bool _smtpLoading = true;
    private SmtpSettingsDto? _smtp;
    private bool _smtpSaving;
    private bool _smtpTesting;
    private bool? _smtpSuccess;
    private string? _smtpMessage;
    private string _testEmail = string.Empty;

    private bool _legalLoading = true;
    private LegalSettingsDto? _legal;
    private bool _legalSaving;
    private bool? _legalSuccess;
    private string? _legalMessage;
    private string _legalTab = "privacy_de";

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        _landingEnabled = Branding.LandingPageEnabled;
        _smtp = await Admin.GetSmtpSettingsAsync() ?? new SmtpSettingsDto();
        _smtpLoading = false;
        _legal = await Admin.GetLegalSettingsAsync() ?? new LegalSettingsDto();
        _legalLoading = false;
    }

    private async Task SaveSmtpAsync()
    {
        if (_smtp == null)
            return;
        _smtpSaving = true;
        _smtpSuccess = null;
        var resp = await Admin.SaveSmtpSettingsAsync(_smtp);
        _smtpSaving = false;
        _smtpSuccess = resp.IsSuccessStatusCode;
        _smtpMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
        _smtp = await Admin.GetSmtpSettingsAsync() ?? _smtp;
    }

    private async Task SaveLegalAsync()
    {
        if (_legal == null)
            return;
        _legalSaving = true;
        _legalSuccess = null;
        var resp = await Admin.SaveLegalSettingsAsync(_legal);
        _legalSaving = false;
        _legalSuccess = resp.IsSuccessStatusCode;
        _legalMessage = resp.IsSuccessStatusCode ? L["common.success"] : L["common.error"];
    }

    private async Task TestSmtpAsync()
    {
        if (string.IsNullOrWhiteSpace(_testEmail))
            return;
        _smtpTesting = true;
        _smtpSuccess = null;
        var resp = await Admin.TestSmtpAsync(_testEmail);
        _smtpTesting = false;
        _smtpSuccess = resp.IsSuccessStatusCode;
        _smtpMessage = resp.IsSuccessStatusCode ? L["admin.settings.smtp.test.ok"] : L["admin.settings.smtp.test.fail"];
    }
}

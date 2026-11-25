using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminSettings : IDisposable
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private BrandingService Branding { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private const long MaxLogoBytes = 4_194_304;
    private const long MaxFaviconBytes = 524_288;

    private static readonly (string Id, string Icon, string Key)[] Sections =
    [
        ("general", "bi-sliders", "admin.settings.general.title"),
        ("branding", "bi-palette", "admin.settings.branding.title"),
        ("email", "bi-envelope-at", "admin.settings.smtp.title"),
        ("ofd", "bi-database", "admin.ofd.sync"),
        ("legal", "bi-file-text", "admin.settings.legal.nav"),
    ];

    private static readonly (string Doc, string Key)[] LegalDocs =
    [
        ("privacy", "legal.privacy.title"),
        ("imprint", "legal.imprint.title"),
        ("terms", "legal.terms.title"),
    ];

    // General
    private bool _landingEnabled = true;
    private bool _landingSaving;
    private bool _landingOk;
    private string? _landingMessage;

    // Branding
    private bool _brandingSaving;
    private bool _brandingOk;
    private string? _brandingMessage;
    private string? _confirmDelete;

    // SMTP
    private bool _smtpLoading = true;
    private SmtpSettingsDto? _smtp;
    private bool _smtpSavedEnabled;
    private bool _smtpSaving;
    private bool _smtpTesting;
    private bool _smtpOk;
    private string? _smtpMessage;
    private string _testEmail = string.Empty;

    // OFD
    private bool _ofdSyncing;
    private bool _ofdOk;
    private string? _ofdMessage;

    // Legal
    private bool _legalLoading = true;
    private LegalSettingsDto? _legal;
    private bool _legalSaving;
    private bool _legalOk;
    private string? _legalMessage;
    private string _legalDoc = "privacy";
    private string _legalLang = "de";
    private bool _legalPreview;

    private string CurrentLegal => GetLegal(_legalDoc, _legalLang);

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        Branding.OnBrandingChanged += OnBrandingChanged;
        _landingEnabled = Branding.LandingPageEnabled;
        _legalLang = L.CurrentLanguage == "en" ? "en" : "de";

        try
        {
            _smtp = await Admin.GetSmtpSettingsAsync() ?? new SmtpSettingsDto();
        }
        catch
        {
            _smtp = null;
            _smtpOk = false;
            _smtpMessage = L["admin.settings.load.failed"];
        }
        _smtpSavedEnabled = _smtp?.IsEnabled == true;
        _smtpLoading = false;

        try
        {
            _legal = await Admin.GetLegalSettingsAsync() ?? new LegalSettingsDto();
        }
        catch
        {
            _legal = null;
        }
        _legalLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        var fragment = new Uri(Nav.Uri).Fragment.TrimStart('#');
        if (!string.IsNullOrEmpty(fragment) && Sections.Any(s => s.Id == fragment))
        {
            await Task.Delay(300);
            await ScrollToAsync(fragment);
        }
    }

    private void OnBrandingChanged() => InvokeAsync(StateHasChanged);

    private async Task ScrollToAsync(string id)
    {
        try
        {
            await JS.InvokeVoidAsync("eval",
                $"document.getElementById('{id}')?.scrollIntoView({{behavior:'smooth',block:'start'}})");
        }
        catch { }
    }

    // ── General ──────────────────────────────────────────────────────────
    private async Task OnLandingChanged(ChangeEventArgs e)
    {
        var value = e.Value is bool b && b;
        _landingSaving = true;
        _landingMessage = null;
        try
        {
            var resp = await Admin.SetLandingPageEnabledAsync(value);
            _landingOk = resp.IsSuccessStatusCode;
            if (_landingOk)
            {
                _landingEnabled = value;
                await Branding.RefreshAsync();
                _landingMessage = value ? L["admin.settings.landing.on"] : L["admin.settings.landing.off"];
            }
            else
            {
                _landingMessage = L["admin.settings.save.failed"];
            }
        }
        catch
        {
            _landingOk = false;
            _landingMessage = L["admin.settings.save.failed"];
        }
        _landingSaving = false;
    }

    // ── Branding ─────────────────────────────────────────────────────────
    private Task OnLogoSelected(InputFileChangeEventArgs e) => UploadAsync(e.File, MaxLogoBytes, Admin.UploadLogoAsync);
    private Task OnLogoDarkSelected(InputFileChangeEventArgs e) => UploadAsync(e.File, MaxLogoBytes, Admin.UploadLogoDarkAsync);
    private Task OnFaviconSelected(InputFileChangeEventArgs e) => UploadAsync(e.File, MaxFaviconBytes, Admin.UploadFaviconAsync);

    private async Task UploadAsync(IBrowserFile file, long maxBytes, Func<MultipartFormDataContent, Task<HttpResponseMessage>> upload)
    {
        _brandingMessage = null;
        if (file.Size > maxBytes)
        {
            _brandingOk = false;
            _brandingMessage = L.Format("admin.settings.branding.too.large", maxBytes >= 1_048_576 ? $"{maxBytes / 1_048_576} MB" : $"{maxBytes / 1024} KB");
            return;
        }

        _brandingSaving = true;
        try
        {
            using var ms = new MemoryStream();
            await file.OpenReadStream(maxBytes).CopyToAsync(ms);
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            using var content = new MultipartFormDataContent();
            content.Add(fileContent, "file", file.Name);
            var resp = await upload(content);
            _brandingOk = resp.IsSuccessStatusCode;
            _brandingMessage = resp.IsSuccessStatusCode ? L["admin.settings.branding.uploaded"] : L["admin.settings.branding.upload.failed"];
            if (resp.IsSuccessStatusCode)
                await Branding.RefreshAsync();
        }
        catch
        {
            _brandingOk = false;
            _brandingMessage = L["admin.settings.branding.upload.failed"];
        }
        _brandingSaving = false;
    }

    private void AskDelete(string what) => _confirmDelete = what;

    private async Task ConfirmDeleteAsync()
    {
        var what = _confirmDelete;
        _confirmDelete = null;
        Func<Task<HttpResponseMessage>> action = what switch
        {
            "logodark" => Admin.DeleteLogoDarkAsync,
            "favicon" => Admin.DeleteFaviconAsync,
            _ => Admin.DeleteLogoAsync
        };

        _brandingMessage = null;
        _brandingSaving = true;
        try
        {
            var resp = await action();
            _brandingOk = resp.IsSuccessStatusCode;
            _brandingMessage = resp.IsSuccessStatusCode ? L["admin.settings.branding.deleted"] : L["admin.settings.save.failed"];
            if (resp.IsSuccessStatusCode)
                await Branding.RefreshAsync();
        }
        catch
        {
            _brandingOk = false;
            _brandingMessage = L["admin.settings.save.failed"];
        }
        _brandingSaving = false;
    }

    // ── SMTP ─────────────────────────────────────────────────────────────
    private async Task SaveSmtpAsync()
    {
        if (_smtp == null)
            return;
        _smtpSaving = true;
        _smtpMessage = null;
        try
        {
            var resp = await Admin.SaveSmtpSettingsAsync(_smtp);
            _smtpOk = resp.IsSuccessStatusCode;
            _smtpMessage = resp.IsSuccessStatusCode ? L["admin.settings.saved"] : L["admin.settings.save.failed"];
            _smtp = await Admin.GetSmtpSettingsAsync() ?? _smtp;
            _smtpSavedEnabled = _smtp.IsEnabled;
        }
        catch
        {
            _smtpOk = false;
            _smtpMessage = L["admin.settings.save.failed"];
        }
        _smtpSaving = false;
    }

    private async Task TestSmtpAsync()
    {
        if (string.IsNullOrWhiteSpace(_testEmail))
            return;
        _smtpTesting = true;
        _smtpMessage = null;
        try
        {
            var resp = await Admin.TestSmtpAsync(_testEmail.Trim());
            _smtpOk = resp.IsSuccessStatusCode;
            _smtpMessage = resp.IsSuccessStatusCode ? L["admin.settings.smtp.test.ok"] : L["admin.settings.smtp.test.fail"];
        }
        catch
        {
            _smtpOk = false;
            _smtpMessage = L["admin.settings.smtp.test.fail"];
        }
        _smtpTesting = false;
    }

    // ── OFD ──────────────────────────────────────────────────────────────
    private record OfdSyncResultDto(int Created, int Updated, int Skipped);

    private async Task SyncOfdAsync()
    {
        _ofdSyncing = true;
        _ofdMessage = null;
        try
        {
            var resp = await Admin.SyncOfdAsync();
            _ofdOk = resp.IsSuccessStatusCode;
            if (_ofdOk)
            {
                var r = await resp.Content.ReadFromJsonAsync<OfdSyncResultDto>();
                _ofdMessage = r == null
                    ? L["admin.ofd.sync.done"]
                    : $"{L["admin.ofd.sync.done"]} {L.Format("admin.ofd.sync.result", r.Created, r.Updated, r.Skipped)}";
            }
            else
            {
                _ofdMessage = L["admin.ofd.sync.failed"];
            }
        }
        catch
        {
            _ofdOk = false;
            _ofdMessage = L["admin.ofd.sync.failed"];
        }
        _ofdSyncing = false;
    }

    // ── Legal ────────────────────────────────────────────────────────────
    private string GetLegal(string doc, string lang)
    {
        if (_legal == null)
            return string.Empty;
        var en = lang == "en";
        return (doc switch
        {
            "imprint" => en ? _legal.ImprintEn : _legal.ImprintDe,
            "terms" => en ? _legal.TermsEn : _legal.TermsDe,
            _ => en ? _legal.PrivacyEn : _legal.PrivacyDe
        }) ?? string.Empty;
    }

    private void OnLegalChanged(ChangeEventArgs e)
    {
        if (_legal == null)
            return;
        var value = e.Value?.ToString() ?? string.Empty;
        var en = _legalLang == "en";
        switch (_legalDoc)
        {
            case "imprint":
                if (en) _legal.ImprintEn = value; else _legal.ImprintDe = value;
                break;
            case "terms":
                if (en) _legal.TermsEn = value; else _legal.TermsDe = value;
                break;
            default:
                if (en) _legal.PrivacyEn = value; else _legal.PrivacyDe = value;
                break;
        }
    }

    private async Task SaveLegalAsync()
    {
        if (_legal == null)
            return;
        _legalSaving = true;
        _legalMessage = null;
        try
        {
            var resp = await Admin.SaveLegalSettingsAsync(_legal);
            _legalOk = resp.IsSuccessStatusCode;
            _legalMessage = resp.IsSuccessStatusCode ? L["admin.settings.legal.saved"] : L["admin.settings.save.failed"];
        }
        catch
        {
            _legalOk = false;
            _legalMessage = L["admin.settings.save.failed"];
        }
        _legalSaving = false;
    }

    public void Dispose() => Branding.OnBrandingChanged -= OnBrandingChanged;
}

using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>
/// Frame for pages that are shown without the signed-in app shell (login, registration, legal pages):
/// brand, theme and language switch, and the footer with the legal links.
/// </summary>
public partial class PublicShell : IDisposable
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private BrandingService Branding { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Optional buttons shown in the header (e.g. "Sign in" on the landing page).</summary>
    [Parameter] public RenderFragment? HeaderActions { get; set; }

    /// <summary>"auth" (centred card), "page" (readable text column) or "landing" (full width).</summary>
    [Parameter] public string Variant { get; set; } = "auth";

    /// <summary>When the user is signed in, the content is rendered without the frame (the app shell already shows it).</summary>
    [Parameter] public bool BypassWhenAuthenticated { get; set; }

    private int _languageKey;

    private string BrandHref => Auth.IsAuthenticated ? "/" : "/login";

    protected override void OnInitialized()
    {
        L.OnLanguageChanged += OnLanguageChanged;
        Branding.OnBrandingChanged += OnBrandingChanged;
    }

    private void OnLanguageChanged()
    {
        _languageKey++;
        InvokeAsync(StateHasChanged);
    }

    private void OnBrandingChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        L.OnLanguageChanged -= OnLanguageChanged;
        Branding.OnBrandingChanged -= OnBrandingChanged;
    }
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Components;

/// <summary>Renders one of the operator's legal texts (privacy, imprint, terms) in the current language.</summary>
public partial class LegalDocument : IDisposable
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;

    /// <summary>"privacy", "imprint" or "terms".</summary>
    [Parameter, EditorRequired] public string Kind { get; set; } = "privacy";

    private bool _loading = true;
    private LegalSettingsDto? _legal;

    private static readonly (string Kind, string Href, string Key)[] All =
    [
        ("privacy", "/privacy", "legal.privacy.title"),
        ("imprint", "/imprint", "legal.imprint.title"),
        ("terms", "/terms", "legal.terms.title"),
    ];

    private string TitleKey => $"legal.{Kind}.title";
    private string SubtitleKey => $"legal.{Kind}.subtitle";

    private IEnumerable<(string Href, string Key)> Related =>
        All.Where(a => a.Kind != Kind).Select(a => (a.Href, a.Key));

    private string? Content
    {
        get
        {
            if (_legal == null)
                return null;
            var en = L.CurrentLanguage == "en";
            return Kind switch
            {
                "imprint" => en ? _legal.ImprintEn : _legal.ImprintDe,
                "terms" => en ? _legal.TermsEn : _legal.TermsDe,
                _ => en ? _legal.PrivacyEn : _legal.PrivacyDe
            };
        }
    }

    protected override async Task OnInitializedAsync()
    {
        L.OnLanguageChanged += OnLanguageChanged;
        try
        {
            _legal = await Http.GetFromJsonAsync<LegalSettingsDto>("api/public/legal");
        }
        catch { }
        _loading = false;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => L.OnLanguageChanged -= OnLanguageChanged;
}

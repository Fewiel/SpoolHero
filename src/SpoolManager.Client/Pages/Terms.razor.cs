using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages;

public partial class Terms
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;

    private bool _loading = true;
    private string _content = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var legal = await Http.GetFromJsonAsync<LegalSettingsDto>("api/public/legal");
            if (legal != null)
                _content = L.CurrentLanguage == "en" ? legal.TermsEn : legal.TermsDe;
        }
        catch { }
        _loading = false;
    }
}

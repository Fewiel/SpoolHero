using Markdig;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages;

public partial class Changelog
{
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private List<string> _entries = [];
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var versions = await Http.GetFromJsonAsync<List<string>>("changelog/index.json") ?? [];
            var lang = L.CurrentLanguage == "de" ? "de" : "en";

            foreach (var v in versions)
            {
                try
                {
                    var md = await Http.GetStringAsync($"changelog/{v}_{lang}.md");
                    _entries.Add(md);
                }
                catch { }
            }
        }
        catch { }
        _loading = false;
    }
}

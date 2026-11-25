using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages;

public partial class Changelog : IDisposable
{
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private const int InitialCount = 5;
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly Regex DateLine = new(@"^\*\*[^*]+:\*\*\s*(?<date>.+)$", RegexOptions.Compiled);

    private sealed record Entry(string Version, string Title, string? Date, string Html);

    private bool _loading = true;
    private bool _showAll;
    private List<Entry> _entries = [];

    private IEnumerable<Entry> VisibleEntries => _showAll ? _entries : _entries.Take(InitialCount);

    protected override async Task OnInitializedAsync()
    {
        L.OnLanguageChanged += OnLanguageChanged;
        await LoadAsync();
    }

    private void OnLanguageChanged() => InvokeAsync(LoadAsync);

    private async Task LoadAsync()
    {
        _loading = true;
        var entries = new List<Entry>();
        try
        {
            var versions = await Http.GetFromJsonAsync<List<string>>("changelog/index.json") ?? [];
            var lang = L.CurrentLanguage == "de" ? "de" : "en";
            var texts = await Task.WhenAll(versions.Select(async v =>
            {
                try { return (v, md: await Http.GetStringAsync($"changelog/{v}_{lang}.md")); }
                catch { return (v, md: (string?)null); }
            }));
            entries.AddRange(texts.Where(t => t.md != null).Select(t => Parse(t.v, t.md!)));
        }
        catch { }
        _entries = entries;
        _loading = false;
        StateHasChanged();
    }

    /// <summary>Splits "# v1.2.3 - Title" and the "**Release date:** …" line off the Markdown body.</summary>
    private static Entry Parse(string version, string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        var title = version;
        var first = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (first >= 0 && lines[first].StartsWith("# ", StringComparison.Ordinal))
        {
            var heading = lines[first][2..].Trim();
            var dash = heading.IndexOf(" - ", StringComparison.Ordinal);
            title = dash >= 0 ? heading[(dash + 3)..].Trim() : heading;
            lines.RemoveAt(first);
        }

        string? date = null;
        var next = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (next >= 0)
        {
            var m = DateLine.Match(lines[next].Trim());
            if (m.Success)
            {
                date = m.Groups["date"].Value.Trim();
                lines.RemoveAt(next);
            }
        }

        var html = Markdown.ToHtml(string.Join('\n', lines), _pipeline);
        // Section headings inside an entry are one level below the entry title (h2)
        html = html.Replace("<h2", "<h3").Replace("</h2>", "</h3>");
        return new Entry(version, title, date, html);
    }

    public void Dispose() => L.OnLanguageChanged -= OnLanguageChanged;
}

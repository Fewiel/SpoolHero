using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminDashboard
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private sealed record Area(string Href, string Icon, string TitleKey, string DescKey);

    private static readonly Area[] Areas =
    [
        new("/admin/users", "bi-people", "admin.users.title", "admin.area.users"),
        new("/admin/materials", "bi-database", "admin.materials.title", "admin.area.materials"),
        new("/admin/suggestions", "bi-lightbulb", "admin.suggestions.title", "admin.area.suggestions"),
        new("/admin/tickets", "bi-ticket-perforated", "admin.tickets.title", "admin.area.tickets"),
        new("/admin/audit", "bi-journal-text", "admin.audit.title", "admin.area.audit"),
        new("/admin/settings", "bi-gear", "admin.settings.title", "admin.area.settings"),
    ];

    private bool _loading = true;
    private AdminStatsDto? _stats;
    private int _pendingSuggestions;

    private int ActiveProjectShare =>
        _stats == null || _stats.TotalProjects == 0 ? 0 : (int)Math.Round(100.0 * _stats.ProjectsWithSpools / _stats.TotalProjects);

    private List<MaterialClickDto> TopClicked =>
        _stats?.TopClickedMaterials.Where(m => m.ClickCount > 0).ToList() ?? [];

    private int BarWidth(int clicks)
    {
        var max = _stats?.TopClickedMaterials.Max(m => m.ClickCount) ?? 0;
        return max == 0 ? 0 : (int)Math.Round(100.0 * clicks / max);
    }

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var statsTask = Admin.GetStatsAsync();
            var suggestionsTask = Admin.GetSuggestionsAsync("pending");
            _stats = await statsTask;
            _pendingSuggestions = (await suggestionsTask)?.Count ?? 0;
        }
        catch
        {
            _stats = null;
        }
        _loading = false;
    }
}

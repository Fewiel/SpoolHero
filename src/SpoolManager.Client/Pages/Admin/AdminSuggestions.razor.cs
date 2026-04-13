using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Suggestions;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminSuggestions
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private bool _reviewing;
    private string _filter = "pending";
    private int _pendingCount;
    private List<MaterialSuggestionDto> _suggestions = [];

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _suggestions = await Admin.GetSuggestionsAsync(_filter) ?? [];
        var allPending = await Admin.GetSuggestionsAsync("pending");
        _pendingCount = allPending?.Count ?? 0;
        _loading = false;
    }

    private async Task SetFilter(string filter)
    {
        _filter = filter;
        await LoadAsync();
    }

    private async Task Review(Guid id, bool approve)
    {
        _reviewing = true;
        await Admin.ReviewSuggestionAsync(id, new ReviewSuggestionRequest
        {
            Status = approve ? "approved" : "rejected"
        });
        _reviewing = false;
        await LoadAsync();
    }
}

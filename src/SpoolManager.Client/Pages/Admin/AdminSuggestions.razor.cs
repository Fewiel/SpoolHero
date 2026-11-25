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
    private bool _loadFailed;
    private bool _reviewing;
    private string _filter = "pending";
    private int _pendingCount;
    private List<MaterialSuggestionDto> _suggestions = [];
    private MaterialSuggestionDto? _reviewTarget;
    private string? _reviewNote;
    private string? _message;
    private bool _messageOk;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loadFailed = false;
        try
        {
            var listTask = Admin.GetSuggestionsAsync(_filter);
            var pendingTask = _filter == "pending" ? listTask : Admin.GetSuggestionsAsync("pending");
            _suggestions = (await listTask ?? []).OrderByDescending(s => s.CreatedAt).ToList();
            _pendingCount = (await pendingTask)?.Count ?? 0;
        }
        catch
        {
            _loadFailed = true;
            _suggestions = [];
        }
        _loading = false;
    }

    private async Task SetFilter(string filter)
    {
        _filter = filter;
        await LoadAsync();
    }

    private void OpenReview(MaterialSuggestionDto s)
    {
        _reviewTarget = s;
        _reviewNote = null;
    }

    private void CloseReview()
    {
        if (!_reviewing)
            _reviewTarget = null;
    }

    private async Task Review(bool approve)
    {
        if (_reviewTarget == null)
            return;
        var target = _reviewTarget;
        _reviewing = true;
        try
        {
            var resp = await Admin.ReviewSuggestionAsync(target.Id, new ReviewSuggestionRequest
            {
                Status = approve ? "approved" : "rejected",
                AdminNotes = string.IsNullOrWhiteSpace(_reviewNote) ? null : _reviewNote.Trim()
            });
            _messageOk = resp.IsSuccessStatusCode;
            _message = resp.IsSuccessStatusCode
                ? L.Format(approve ? "admin.suggestions.approved.msg" : "admin.suggestions.rejected.msg", $"{target.Brand} {target.Type}")
                : L["admin.suggestions.review.failed"];
        }
        catch
        {
            _messageOk = false;
            _message = L["admin.suggestions.review.failed"];
        }
        _reviewing = false;
        _reviewTarget = null;
        await LoadAsync();
    }
}

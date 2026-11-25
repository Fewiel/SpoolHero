using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminTickets : IDisposable
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private const int PageSize = 25;

    private static readonly (string? Value, string Key)[] Filters =
    [
        (null, "spool.filter.all"),
        ("open", "ticket.status.open"),
        ("in_progress", "ticket.status.in_progress"),
        ("answered", "ticket.status.answered"),
        ("closed", "ticket.status.closed"),
    ];

    private bool _loading = true;
    private bool _loadFailed;
    private List<SupportTicketDto> _tickets = [];
    private string? _statusFilter;
    private string _search = string.Empty;
    private string? _error;
    private Guid? _busyId;
    private int _page;
    private System.Timers.Timer? _searchTimer;

    private IEnumerable<SupportTicketDto> PagedTickets => _tickets.Skip(_page * PageSize).Take(PageSize);

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
            _tickets = await Admin.GetTicketsAsync(
                _statusFilter,
                string.IsNullOrWhiteSpace(_search) ? null : _search.Trim()) ?? [];
        }
        catch
        {
            _loadFailed = true;
            _tickets = [];
        }
        var maxPage = Math.Max(0, (int)Math.Ceiling(_tickets.Count / (double)PageSize) - 1);
        _page = Math.Min(_page, maxPage);
        _loading = false;
    }

    private async Task SetFilter(string? status)
    {
        _statusFilter = status;
        _page = 0;
        await LoadAsync();
    }

    private async Task ResetFiltersAsync()
    {
        _statusFilter = null;
        _search = string.Empty;
        _page = 0;
        await LoadAsync();
    }

    private void OnSearch(string value)
    {
        _search = value;
        _page = 0;
        _searchTimer?.Stop();
        _searchTimer?.Dispose();
        _searchTimer = new System.Timers.Timer(300) { AutoReset = false };
        _searchTimer.Elapsed += async (_, _) => await InvokeAsync(async () => { await LoadAsync(); StateHasChanged(); });
        _searchTimer.Start();
    }

    private async Task RunAsync(SupportTicketDto ticket, Func<Task<HttpResponseMessage>> action)
    {
        _busyId = ticket.Id;
        _error = null;
        try
        {
            var resp = await action();
            if (!resp.IsSuccessStatusCode)
                _error = L["admin.tickets.action.failed"];
        }
        catch
        {
            _error = L["admin.tickets.action.failed"];
        }
        _busyId = null;
        await LoadAsync();
    }

    private Task SetStatusAsync(SupportTicketDto ticket, string status) =>
        RunAsync(ticket, () => Admin.SetTicketStatusAsync(ticket.Id, status));

    private Task AssignMeAsync(SupportTicketDto ticket) =>
        Auth.CurrentUser == null ? Task.CompletedTask : RunAsync(ticket, () => Admin.AssignTicketAsync(ticket.Id, Auth.CurrentUser.Id));

    private Task UnassignAsync(SupportTicketDto ticket) =>
        RunAsync(ticket, () => Admin.AssignTicketAsync(ticket.Id, null));

    private static string StatusChipClass(string status) => status switch
    {
        "in_progress" => "sh-chip-accent",
        "answered" => "sh-chip-ok",
        "closed" => string.Empty,
        _ => "sh-chip-warn"
    };

    private string StatusLabel(string status) => status switch
    {
        "in_progress" => L["ticket.status.in_progress"],
        "answered" => L["ticket.status.answered"],
        "closed" => L["ticket.status.closed"],
        _ => L["ticket.status.open"]
    };

    public void Dispose() => _searchTimer?.Dispose();
}

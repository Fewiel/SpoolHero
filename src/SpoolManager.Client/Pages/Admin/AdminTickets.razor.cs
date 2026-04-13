using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Pages.Admin;

public partial class AdminTickets : IDisposable
{
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private List<SupportTicketDto> _tickets = [];
    private string? _statusFilter;
    private string _search = string.Empty;
    private System.Timers.Timer? _searchTimer;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentUser?.IsPlatformAdmin != true)
            return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _tickets = await Admin.GetTicketsAsync(
            _statusFilter,
            string.IsNullOrWhiteSpace(_search) ? null : _search) ?? [];
        _loading = false;
    }

    private async Task SetFilter(string? status)
    {
        _statusFilter = status;
        await LoadAsync();
    }

    private void OnSearch(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? string.Empty;
        _searchTimer?.Stop();
        _searchTimer = new System.Timers.Timer(300);
        _searchTimer.Elapsed += async (_, _) => { _searchTimer?.Stop(); await InvokeAsync(LoadAsync); };
        _searchTimer.Start();
    }

    private async Task SetStatusAsync(SupportTicketDto ticket, string status)
    {
        await Admin.SetTicketStatusAsync(ticket.Id, status);
        await LoadAsync();
    }

    private async Task AssignMeAsync(SupportTicketDto ticket)
    {
        if (Auth.CurrentUser == null)
            return;
        await Admin.AssignTicketAsync(ticket.Id, Auth.CurrentUser.Id);
        await LoadAsync();
    }

    private async Task UnassignAsync(SupportTicketDto ticket)
    {
        await Admin.AssignTicketAsync(ticket.Id, null);
        await LoadAsync();
    }

    private static string StatusBadgeClass(string status) => status switch
    {
        "in_progress" => "bg-primary",
        "answered"    => "bg-success",
        "closed"      => "bg-secondary",
        _             => "bg-warning text-dark"
    };

    private string StatusLabel(string status) => status switch
    {
        "in_progress" => L["ticket.status.in_progress"],
        "answered"    => L["ticket.status.answered"],
        "closed"      => L["ticket.status.closed"],
        _             => L["ticket.status.open"]
    };

    public void Dispose() => _searchTimer?.Dispose();
}

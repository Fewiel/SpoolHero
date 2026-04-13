using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Pages.Tickets;

public partial class TicketDetail
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private AdminService Admin { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private TicketDetailDto? _ticket;
    private string _comment = string.Empty;
    private bool _isInternal;
    private bool _sending;
    private bool _confirmClose;

    private bool IsAdmin => Auth.CurrentUser?.IsPlatformAdmin == true;
    private string BackUrl => IsAdmin ? "/admin/tickets" : "/tickets";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _ticket = await Tickets.GetDetailAsync(Id);
        _loading = false;
    }

    private async Task SetStatusAsync(ChangeEventArgs e)
    {
        if (_ticket == null)
            return;
        await Admin.SetTicketStatusAsync(Id, e.Value?.ToString() ?? _ticket.Status);
        await LoadAsync();
    }

    private async Task AssignMeAsync()
    {
        if (Auth.CurrentUser == null)
            return;
        await Admin.AssignTicketAsync(Id, Auth.CurrentUser.Id);
        await LoadAsync();
    }

    private async Task UnassignAsync()
    {
        await Admin.AssignTicketAsync(Id, null);
        await LoadAsync();
    }

    private async Task SendReplyAsync()
    {
        if (string.IsNullOrWhiteSpace(_comment))
            return;
        _sending = true;
        if (IsAdmin)
            await Admin.AddAdminCommentAsync(Id, _comment.Trim(), _isInternal);
        else
            await Tickets.AddCommentAsync(Id, _comment.Trim());
        _comment = string.Empty;
        _isInternal = false;
        _sending = false;
        await LoadAsync();
    }

    private async Task ConfirmCloseAsync()
    {
        _confirmClose = false;
        await Tickets.CloseAsync(Id);
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
}

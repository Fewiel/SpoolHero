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
    private string? _sendError;

    private bool IsMine(Guid userId) => userId == Auth.CurrentUser?.Id;

    private static string Initial(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

    private bool IsAdmin => Auth.CurrentUser?.IsPlatformAdmin == true;
    private string BackUrl => IsAdmin ? "/admin/tickets" : "/tickets";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = _ticket == null;
        try
        {
            _ticket = await Tickets.GetDetailAsync(Id);
        }
        catch (HttpRequestException)
        {
            _ticket = null;
        }
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
        _sendError = null;
        try
        {
            var resp = IsAdmin
                ? await Admin.AddAdminCommentAsync(Id, _comment.Trim(), _isInternal)
                : await Tickets.AddCommentAsync(Id, _comment.Trim());
            if (!resp.IsSuccessStatusCode)
            {
                _sendError = await ResponseMessages.ReadAsync(resp, L["ticket.send.error"]);
                return;
            }
            _comment = string.Empty;
            _isInternal = false;
            await LoadAsync();
        }
        catch (HttpRequestException)
        {
            _sendError = L["ticket.send.error"];
        }
        finally
        {
            _sending = false;
        }
    }

    private async Task ConfirmCloseAsync()
    {
        _confirmClose = false;
        await Tickets.CloseAsync(Id);
        await LoadAsync();
    }
}

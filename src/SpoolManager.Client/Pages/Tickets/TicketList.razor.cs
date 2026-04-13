using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Pages.Tickets;

public partial class TicketList
{
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private bool _showForm;
    private bool _creating;
    private string? _createError;
    private List<SupportTicketDto> _tickets = [];
    private CreateTicketRequest _form = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _tickets = await Tickets.GetMyTicketsAsync() ?? [];
        _loading = false;
    }

    private async Task CreateAsync()
    {
        _creating = true;
        _createError = null;
        var resp = await Tickets.CreateAsync(_form);
        if (resp.IsSuccessStatusCode)
        {
            _showForm = false;
            _form = new();
            await LoadAsync();
        }
        else
        {
            _createError = L["common.error"];
        }
        _creating = false;
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

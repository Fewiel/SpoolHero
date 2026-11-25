using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Tickets;

namespace SpoolManager.Client.Pages.Tickets;

public partial class TicketList
{
    [Inject] private TicketService Tickets { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;

    private bool _loading = true;
    private bool _loadError;
    private bool _showForm;
    private bool _creating;
    private bool _created;
    private string? _createError;
    private string _filter = "active";
    private List<SupportTicketDto> _tickets = [];
    private CreateTicketRequest _form = new();

    private List<SupportTicketDto> Filtered => _filter switch
    {
        "closed" => _tickets.Where(t => t.Status == "closed").ToList(),
        "all" => _tickets,
        _ => _tickets.Where(t => t.Status != "closed").ToList()
    };

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loadError = false;
        try
        {
            _tickets = (await Tickets.GetMyTicketsAsync() ?? [])
                .OrderBy(t => t.Status == "closed")
                .ThenByDescending(t => t.UpdatedAt)
                .ToList();
        }
        catch (HttpRequestException)
        {
            _loadError = true;
        }
        _loading = false;
    }

    private void OpenForm()
    {
        _created = false;
        _createError = null;
        _showForm = true;
    }

    private async Task CreateAsync()
    {
        _creating = true;
        _createError = null;
        try
        {
            var resp = await Tickets.CreateAsync(_form);
            if (resp.IsSuccessStatusCode)
            {
                _showForm = false;
                _form = new();
                _created = true;
                _filter = "active";
                await LoadAsync();
            }
            else
            {
                _createError = await ResponseMessages.ReadAsync(resp, L["common.error"]);
            }
        }
        catch (HttpRequestException)
        {
            _createError = L["common.error"];
        }
        finally
        {
            _creating = false;
        }
    }
}

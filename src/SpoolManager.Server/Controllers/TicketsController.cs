using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Tickets;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketRepository _tickets;
    private readonly IUserRepository _users;
    private readonly IEmailService _email;

    public TicketsController(ITicketRepository tickets, IUserRepository users, IEmailService email)
    {
        _tickets = tickets;
        _users = users;
        _email = email;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string UserName => User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    private bool IsPlatformAdmin() => User.FindFirst("is_platform_admin")?.Value == "true";

    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject)) return BadRequest("Subject required.");
        if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest("Description required.");

        var ticket = new SupportTicket
        {
            UserId = UserId,
            Username = UserName,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open
        };
        var id = await _tickets.CreateAsync(ticket);
        ticket.Id = id;

        var admins = await _users.GetAdminsAsync();
        await _email.NotifyAdminsNewTicketAsync(ticket, admins);

        return Ok(new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyTickets()
    {
        var tickets = await _tickets.GetByUserAsync(UserId);
        var result = new List<SupportTicketDto>();
        foreach (var t in tickets)
        {
            var comments = await _tickets.GetCommentsAsync(t.Id);
            var publicCount = comments.Count(c => !c.IsInternal);
            result.Add(AdminController.MapTicketToDto(t, publicCount));
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.UserId != UserId && !IsPlatformAdmin()) return Forbid();

        var isAdmin = IsPlatformAdmin();
        var comments = await _tickets.GetCommentsAsync(id);
        var visibleComments = comments.Where(c => !c.IsInternal || isAdmin).ToList();

        var dto = new TicketDetailDto
        {
            Id = ticket.Id, UserId = ticket.UserId, Username = ticket.Username,
            Subject = ticket.Subject, Description = ticket.Description,
            Status = ticket.Status switch
            {
                TicketStatus.InProgress => "in_progress",
                TicketStatus.Closed => "closed",
                TicketStatus.Answered => "answered",
                _ => "open"
            },
            AssignedToUserId = ticket.AssignedToUserId,
            AssignedToUsername = ticket.AssignedToUsername,
            CommentCount = comments.Count(c => !c.IsInternal),
            CreatedAt = ticket.CreatedAt, UpdatedAt = ticket.UpdatedAt,
            Comments = visibleComments.Select(c => new TicketCommentDto
            {
                Id = c.Id, UserId = c.UserId, Username = c.Username,
                IsAdmin = c.IsAdmin, IsInternal = c.IsInternal,
                Content = c.Content, CreatedAt = c.CreatedAt
            }).ToList()
        };
        return Ok(dto);
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseTicket(Guid id)
    {
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.UserId != UserId) return Forbid();
        if (ticket.Status == TicketStatus.Closed) return BadRequest("Ticket is already closed.");

        ticket.Status = TicketStatus.Closed;
        await _tickets.UpdateAsync(ticket);
        return NoContent();
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content)) return BadRequest("Content required.");
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.UserId != UserId && !IsPlatformAdmin()) return Forbid();

        var comment = new TicketComment
        {
            TicketId = id, UserId = UserId,
            Username = UserName, IsAdmin = IsPlatformAdmin(),
            IsInternal = false,
            Content = request.Content.Trim()
        };
        await _tickets.AddCommentAsync(comment);

        if (!IsPlatformAdmin() && ticket.Status == TicketStatus.Answered)
            ticket.Status = TicketStatus.Open;

        await _tickets.UpdateAsync(ticket);

        if (!IsPlatformAdmin() && ticket.AssignedToUserId.HasValue && ticket.AssignedToUserId.Value != UserId)
        {
            var assignedAdmin = await _users.GetByIdAsync(ticket.AssignedToUserId.Value);
            if (assignedAdmin != null)
                await _email.NotifyTicketReplyAsync(ticket, assignedAdmin, request.Content.Trim(), replyIsFromAdmin: false);
        }

        return NoContent();
    }
}

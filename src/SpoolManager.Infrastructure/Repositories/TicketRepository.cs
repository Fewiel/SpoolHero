using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ITicketRepository
{
    Task<List<SupportTicket>> GetByUserAsync(Guid userId);
    Task<List<SupportTicket>> GetAllAsync(TicketStatus? status = null, string? search = null);
    Task<SupportTicket?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(SupportTicket ticket);
    Task UpdateAsync(SupportTicket ticket);
    Task<List<TicketComment>> GetCommentsAsync(Guid ticketId);
    Task<Guid> AddCommentAsync(TicketComment comment);
    Task<int> CountByStatusAsync(TicketStatus status);
}

public class TicketRepository : ITicketRepository
{
    private readonly SpoolManagerDb _db;

    public TicketRepository(SpoolManagerDb db) => _db = db;

    public async Task<List<SupportTicket>> GetByUserAsync(Guid userId) =>
        await _db.Tickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

    public async Task<List<SupportTicket>> GetAllAsync(TicketStatus? status = null, string? search = null)
    {
        var query = _db.Tickets.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Subject.Contains(search) || t.Username.Contains(search));

        return await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
    }

    public async Task<SupportTicket?> GetByIdAsync(Guid id) =>
        await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Guid> CreateAsync(SupportTicket ticket)
    {
        ticket.Id = Guid.NewGuid();
        ticket.CreatedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(ticket);
        return ticket.Id;
    }

    public async Task UpdateAsync(SupportTicket ticket)
    {
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.UpdateAsync(ticket);
    }

    public async Task<List<TicketComment>> GetCommentsAsync(Guid ticketId) =>
        await _db.TicketComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    public async Task<Guid> AddCommentAsync(TicketComment comment)
    {
        comment.Id = Guid.NewGuid();
        comment.CreatedAt = DateTime.UtcNow;
        await _db.InsertAsync(comment);
        return comment.Id;
    }

    public async Task<int> CountByStatusAsync(TicketStatus status) =>
        await _db.Tickets.CountAsync(t => t.Status == status);
}

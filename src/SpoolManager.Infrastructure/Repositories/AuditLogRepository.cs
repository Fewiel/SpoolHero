using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IAuditLogRepository
{
    Task<List<AuditLog>> GetRecentAsync(int limit = 50, int offset = 0, string? actionFilter = null, string? userFilter = null);
    Task<int> CountAsync(string? actionFilter = null, string? userFilter = null);
    Task CreateAsync(AuditLog log);
    Task<int> DeleteOlderThanAsync(DateTime cutoff);
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly SpoolManagerDb _db;

    public AuditLogRepository(SpoolManagerDb db) => _db = db;

    public async Task<List<AuditLog>> GetRecentAsync(int limit = 50, int offset = 0, string? actionFilter = null, string? userFilter = null)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(l => l.Action.Contains(actionFilter));

        if (!string.IsNullOrWhiteSpace(userFilter))
            query = query.Where(l => l.Username != null && l.Username.Contains(userFilter));

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> CountAsync(string? actionFilter = null, string? userFilter = null)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(l => l.Action.Contains(actionFilter));

        if (!string.IsNullOrWhiteSpace(userFilter))
            query = query.Where(l => l.Username != null && l.Username.Contains(userFilter));

        return await query.CountAsync();
    }

    public async Task CreateAsync(AuditLog log)
    {
        log.Id = Guid.NewGuid();
        await _db.InsertAsync(log);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff)
    {
        return await _db.AuditLogs
            .Where(l => l.Timestamp < cutoff)
            .DeleteAsync();
    }
}

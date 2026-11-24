using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISpoolmanCallLogRepository
{
    Task LogAsync(Guid apiKeyId, string method, string path, int statusCode);
    Task<List<SpoolmanCallLog>> GetLast24hAsync(Guid apiKeyId, int limit = 500);
    Task DeleteOlderThanAsync(DateTime cutoff);
}

public class SpoolmanCallLogRepository : ISpoolmanCallLogRepository
{
    private readonly SpoolManagerDb _db;

    public SpoolmanCallLogRepository(SpoolManagerDb db) => _db = db;

    public async Task LogAsync(Guid apiKeyId, string method, string path, int statusCode)
    {
        await _db.InsertAsync(new SpoolmanCallLog
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            CalledAt = DateTime.UtcNow,
            Method = method.Length > 10 ? method[..10] : method,
            Path = path.Length > 255 ? path[..255] : path,
            StatusCode = statusCode,
        });
    }

    public async Task<List<SpoolmanCallLog>> GetLast24hAsync(Guid apiKeyId, int limit = 500)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return await _db.SpoolmanCallLogs
            .Where(l => l.ApiKeyId == apiKeyId && l.CalledAt >= cutoff)
            .OrderByDescending(l => l.CalledAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task DeleteOlderThanAsync(DateTime cutoff) =>
        await _db.SpoolmanCallLogs.Where(l => l.CalledAt < cutoff).DeleteAsync();
}

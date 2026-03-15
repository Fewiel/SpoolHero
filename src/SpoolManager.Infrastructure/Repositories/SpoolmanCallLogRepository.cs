using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISpoolmanCallLogRepository
{
    Task LogAsync(Guid apiKeyId, string method, string path, int statusCode);
    Task<List<SpoolmanCallLog>> GetLast24hAsync(Guid apiKeyId);
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
            Method = method,
            Path = path,
            StatusCode = statusCode,
        });
    }

    public async Task<List<SpoolmanCallLog>> GetLast24hAsync(Guid apiKeyId)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return await _db.SpoolmanCallLogs
            .Where(l => l.ApiKeyId == apiKeyId && l.CalledAt >= cutoff)
            .OrderByDescending(l => l.CalledAt)
            .ToListAsync();
    }
}

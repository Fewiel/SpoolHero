using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISpoolmanApiKeyRepository
{
    Task<SpoolmanApiKey?> GetByApiKeyAsync(string apiKey);
    Task<List<SpoolmanApiKey>> GetAllByProjectAsync(Guid projectId);
    Task<Guid> CreateAsync(SpoolmanApiKey key);
    Task DeleteAsync(Guid id);
    Task UpdateLastUsedAsync(Guid id);
}

public class SpoolmanApiKeyRepository : ISpoolmanApiKeyRepository
{
    private readonly SpoolManagerDb _db;

    public SpoolmanApiKeyRepository(SpoolManagerDb db) => _db = db;

    public async Task<SpoolmanApiKey?> GetByApiKeyAsync(string apiKey) =>
        await _db.SpoolmanApiKeys.FirstOrDefaultAsync(k => k.ApiKey == apiKey);

    public async Task<List<SpoolmanApiKey>> GetAllByProjectAsync(Guid projectId) =>
        await _db.SpoolmanApiKeys
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();

    public async Task<Guid> CreateAsync(SpoolmanApiKey key)
    {
        key.Id = Guid.NewGuid();
        key.CreatedAt = DateTime.UtcNow;
        await _db.InsertAsync(key);
        return key.Id;
    }

    public async Task DeleteAsync(Guid id) =>
        await _db.SpoolmanApiKeys.Where(k => k.Id == id).DeleteAsync();

    public async Task UpdateLastUsedAsync(Guid id) =>
        await _db.SpoolmanApiKeys
            .Where(k => k.Id == id)
            .Set(k => k.LastUsedAt, DateTime.UtcNow)
            .UpdateAsync();
}

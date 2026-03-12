using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISiteSettingsRepository
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);
}

public class SiteSettingsRepository : ISiteSettingsRepository
{
    private readonly SpoolManagerDb _db;

    public SiteSettingsRepository(SpoolManagerDb db) => _db = db;

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == key);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string? value)
    {
        var existing = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (existing != null)
        {
            await _db.SiteSettings
                .Where(x => x.Key == key)
                .Set(x => x.Value, value)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync();
        }
        else
        {
            await _db.InsertAsync(new SiteSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
    }
}

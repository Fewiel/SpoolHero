using LinqToDB;
using SpoolManager.Infrastructure.Data;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISmtpSettingsRepository
{
    Task<SmtpSettings?> GetAsync();
    Task SaveAsync(SmtpSettings settings);
}

public class SmtpSettingsRepository : ISmtpSettingsRepository
{
    private readonly SpoolManagerDb _db;

    public SmtpSettingsRepository(SpoolManagerDb db) => _db = db;

    public async Task<SmtpSettings?> GetAsync() =>
        await _db.SmtpSettings.FirstOrDefaultAsync();

    public async Task SaveAsync(SmtpSettings settings)
    {
        var existing = await _db.SmtpSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            settings.Id = Guid.NewGuid();
            await _db.InsertAsync(settings);
        }
        else
        {
            settings.Id = existing.Id;
            await _db.UpdateAsync(settings);
        }
    }
}

using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IDryerRepository
{
    Task<List<Dryer>> GetAllAsync(Guid projectId);
    Task<List<Dryer>> GetAllForInventoryAsync();
    Task<Dryer?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Dryer dryer);
    Task UpdateAsync(Dryer dryer);
    Task DeleteAsync(Guid id);
    Task<List<Dryer>> GetDryersNeedingNotificationAsync();
}

public class DryerRepository : IDryerRepository
{
    private readonly SpoolManagerDb _db;

    public DryerRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<Dryer>> GetAllAsync(Guid projectId) =>
        await _db.Dryers.Where(d => d.ProjectId == projectId).OrderBy(d => d.Name).ToListAsync();

    public async Task<List<Dryer>> GetAllForInventoryAsync() =>
        await _db.Dryers.OrderBy(d => d.Name).ToListAsync();

    public async Task<Dryer?> GetByIdAsync(Guid id) =>
        await _db.Dryers.FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Guid> CreateAsync(Dryer dryer)
    {
        dryer.Id = Guid.NewGuid();
        dryer.CreatedAt = DateTime.UtcNow;
        dryer.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(dryer);
        return dryer.Id;
    }

    public async Task UpdateAsync(Dryer dryer) =>
        await _db.UpdateAsync(dryer);

    public async Task DeleteAsync(Guid id) =>
        await _db.Dryers.Where(d => d.Id == id).DeleteAsync();

    public async Task<List<Dryer>> GetDryersNeedingNotificationAsync()
    {
        var now = DateTime.UtcNow;
        return await _db.Dryers
            .Where(d => d.IsDrying && d.DryingFinishAt <= now && d.DryingNotifiedAt == null)
            .ToListAsync();
    }
}

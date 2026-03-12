using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IStorageLocationRepository
{
    Task<List<StorageLocation>> GetAllAsync(Guid projectId);
    Task<List<StorageLocation>> GetAllForInventoryAsync();
    Task<StorageLocation?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(StorageLocation location);
    Task UpdateAsync(StorageLocation location);
    Task DeleteAsync(Guid id);
}

public class StorageLocationRepository : IStorageLocationRepository
{
    private readonly SpoolManagerDb _db;

    public StorageLocationRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<StorageLocation>> GetAllAsync(Guid projectId) =>
        await _db.StorageLocations.Where(sl => sl.ProjectId == projectId).OrderBy(sl => sl.Name).ToListAsync();

    public async Task<List<StorageLocation>> GetAllForInventoryAsync() =>
        await _db.StorageLocations.OrderBy(sl => sl.Name).ToListAsync();

    public async Task<StorageLocation?> GetByIdAsync(Guid id) =>
        await _db.StorageLocations.FirstOrDefaultAsync(sl => sl.Id == id);

    public async Task<Guid> CreateAsync(StorageLocation location)
    {
        location.Id = Guid.NewGuid();
        location.CreatedAt = DateTime.UtcNow;
        location.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(location);
        return location.Id;
    }

    public async Task UpdateAsync(StorageLocation location) =>
        await _db.UpdateAsync(location);

    public async Task DeleteAsync(Guid id) =>
        await _db.StorageLocations.Where(sl => sl.Id == id).DeleteAsync();
}

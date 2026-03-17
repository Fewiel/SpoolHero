using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISlicerProfileRepository
{
    Task<List<SlicerProfile>> GetByMaterialAsync(Guid materialId);
    Task<List<SlicerProfile>> GetByMaterialAndPrinterAsync(Guid materialId, Guid printerId);
    Task<SlicerProfile?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(SlicerProfile profile);
    Task UpdateAsync(SlicerProfile profile);
    Task DeleteAsync(Guid id);
}

public class SlicerProfileRepository : ISlicerProfileRepository
{
    private readonly SpoolManagerDb _db;

    public SlicerProfileRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<SlicerProfile>> GetByMaterialAsync(Guid materialId) =>
        await _db.SlicerProfiles
            .Where(p => p.FilamentMaterialId == materialId)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<List<SlicerProfile>> GetByMaterialAndPrinterAsync(Guid materialId, Guid printerId) =>
        await _db.SlicerProfiles
            .Where(p => p.FilamentMaterialId == materialId && (p.PrinterId == null || p.PrinterId == printerId))
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<SlicerProfile?> GetByIdAsync(Guid id) =>
        await _db.SlicerProfiles.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Guid> CreateAsync(SlicerProfile profile)
    {
        profile.Id = Guid.NewGuid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(profile);
        return profile.Id;
    }

    public async Task UpdateAsync(SlicerProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.UpdateAsync(profile);
    }

    public async Task DeleteAsync(Guid id) =>
        await _db.SlicerProfiles.Where(p => p.Id == id).DeleteAsync();
}

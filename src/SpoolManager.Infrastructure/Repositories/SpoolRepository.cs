using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISpoolRepository
{
    Task<List<Spool>> GetAllAsync(Guid projectId, Guid? materialId = null, Guid? printerId = null, Guid? storageId = null, Guid? dryerId = null, bool? consumed = null, string? search = null);
    Task<Spool?> GetByIdAsync(Guid id, Guid projectId);
    Task<Spool?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Spool spool);
    Task UpdateAsync(Spool spool);
    Task DeleteAsync(Guid id);
    Task<int> GetTotalCountAsync();
    Task<List<Spool>> GetSpoolsNeedingLowNotificationAsync();
    Task<Spool?> GetBySpoolmanIdAsync(int spoolmanId, Guid projectId);
    Task<List<Spool>> GetAllByProjectAsync(Guid projectId);
    Task UpdateRemainingWeightAtomicAsync(Guid spoolId, decimal subtractGrams, decimal totalWeightGrams);
}

public class SpoolRepository : ISpoolRepository
{
    private readonly SpoolManagerDb _db;

    public SpoolRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<Spool>> GetAllAsync(Guid projectId, Guid? materialId = null, Guid? printerId = null, Guid? storageId = null, Guid? dryerId = null, bool? consumed = null, string? search = null)
    {
        var query = _db.Spools.Where(s => s.ProjectId == projectId);

        if (materialId.HasValue)
            query = query.Where(s => s.FilamentMaterialId == materialId.Value);

        if (printerId.HasValue)
            query = query.Where(s => s.PrinterId == printerId.Value);

        if (storageId.HasValue)
            query = query.Where(s => s.StorageLocationId == storageId.Value);

        if (dryerId.HasValue)
            query = query.Where(s => s.DryerId == dryerId.Value);

        if (consumed.HasValue)
            query = query.Where(s => consumed.Value ? s.ConsumedAt != null : s.ConsumedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingMaterialIds = await _db.FilamentMaterials
                .Where(m => m.Brand.Contains(search) || m.Type.Contains(search))
                .Select(m => m.Id).ToListAsync();
            query = query.Where(s => (s.RfidTagUid != null && s.RfidTagUid.Contains(search)) || matchingMaterialIds.Contains(s.FilamentMaterialId));
        }

        var spools = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

        if (spools.Count > 0)
        {
            var materialIds = spools.Select(s => s.FilamentMaterialId).Distinct().ToList();
            var materials = await _db.FilamentMaterials.Where(m => materialIds.Contains(m.Id)).ToListAsync();
            var materialMap = materials.ToDictionary(m => m.Id);

            var printerIds = spools.Where(s => s.PrinterId.HasValue).Select(s => s.PrinterId!.Value).Distinct().ToList();
            var printers = printerIds.Count > 0 ? await _db.Printers.Where(p => printerIds.Contains(p.Id)).ToListAsync() : new List<Printer>();
            var printerMap = printers.ToDictionary(p => p.Id);

            var storageIds = spools.Where(s => s.StorageLocationId.HasValue).Select(s => s.StorageLocationId!.Value).Distinct().ToList();
            var storages = storageIds.Count > 0 ? await _db.StorageLocations.Where(sl => storageIds.Contains(sl.Id)).ToListAsync() : new List<StorageLocation>();
            var storageMap = storages.ToDictionary(sl => sl.Id);

            var dryerIds = spools.Where(s => s.DryerId.HasValue).Select(s => s.DryerId!.Value).Distinct().ToList();
            var dryers = dryerIds.Count > 0 ? await _db.Dryers.Where(d => dryerIds.Contains(d.Id)).ToListAsync() : new List<Dryer>();
            var dryerMap = dryers.ToDictionary(d => d.Id);

            foreach (var spool in spools)
            {
                if (materialMap.TryGetValue(spool.FilamentMaterialId, out var mat))
                    spool.FilamentMaterial = mat;
                if (spool.PrinterId.HasValue && printerMap.TryGetValue(spool.PrinterId.Value, out var prt))
                    spool.Printer = prt;
                if (spool.StorageLocationId.HasValue && storageMap.TryGetValue(spool.StorageLocationId.Value, out var stg))
                    spool.StorageLocation = stg;
                if (spool.DryerId.HasValue && dryerMap.TryGetValue(spool.DryerId.Value, out var dry))
                    spool.Dryer = dry;
            }
        }

        return spools;
    }

    public async Task<Spool?> GetByIdAsync(Guid id, Guid projectId)
    {
        var spool = await _db.Spools.FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
        return await LoadNavPropsAsync(spool);
    }

    public async Task<Spool?> GetByIdAsync(Guid id)
    {
        var spool = await _db.Spools.FirstOrDefaultAsync(s => s.Id == id);
        return await LoadNavPropsAsync(spool);
    }

    private async Task<Spool?> LoadNavPropsAsync(Spool? spool)
    {
        if (spool == null) return null;
        spool.FilamentMaterial = await _db.FilamentMaterials.FirstOrDefaultAsync(m => m.Id == spool.FilamentMaterialId);
        if (spool.PrinterId.HasValue)
            spool.Printer = await _db.Printers.FirstOrDefaultAsync(p => p.Id == spool.PrinterId.Value);
        if (spool.StorageLocationId.HasValue)
            spool.StorageLocation = await _db.StorageLocations.FirstOrDefaultAsync(sl => sl.Id == spool.StorageLocationId.Value);
        if (spool.DryerId.HasValue)
            spool.Dryer = await _db.Dryers.FirstOrDefaultAsync(d => d.Id == spool.DryerId.Value);
        return spool;
    }

    public async Task<Guid> CreateAsync(Spool spool)
    {
        spool.Id = Guid.NewGuid();
        spool.CreatedAt = DateTime.UtcNow;
        spool.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(spool);
        return spool.Id;
    }

    public async Task UpdateAsync(Spool spool) =>
        await _db.UpdateAsync(spool);

    public async Task DeleteAsync(Guid id) =>
        await _db.Spools.Where(s => s.Id == id).DeleteAsync();

    public async Task<int> GetTotalCountAsync() =>
        await _db.Spools.CountAsync();

    public async Task<Spool?> GetBySpoolmanIdAsync(int spoolmanId, Guid projectId)
    {
        var spool = await _db.Spools.FirstOrDefaultAsync(s => s.SpoolmanId == spoolmanId && s.ProjectId == projectId);
        return await LoadNavPropsAsync(spool);
    }

    public async Task<List<Spool>> GetAllByProjectAsync(Guid projectId)
    {
        var spools = await _db.Spools
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        foreach (var spool in spools)
            spool.FilamentMaterial = await _db.FilamentMaterials.FirstOrDefaultAsync(m => m.Id == spool.FilamentMaterialId);

        return spools;
    }

    public async Task UpdateRemainingWeightAtomicAsync(Guid spoolId, decimal subtractGrams, decimal totalWeightGrams)
    {
        await _db.Spools
            .Where(s => s.Id == spoolId)
            .Set(s => s.RemainingWeightGrams, s => s.RemainingWeightGrams - subtractGrams < 0 ? 0 : s.RemainingWeightGrams - subtractGrams)
            .Set(s => s.RemainingPercent, s => totalWeightGrams > 0
                ? (s.RemainingWeightGrams - subtractGrams < 0 ? 0 : (s.RemainingWeightGrams - subtractGrams) / totalWeightGrams * 100)
                : 0)
            .Set(s => s.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();
    }

    public async Task<List<Spool>> GetSpoolsNeedingLowNotificationAsync()
    {
        var spools = await _db.Spools
            .Where(s => s.ConsumedAt == null && s.RemainingPercent <= 15 && s.LowSpoolNotifiedAt == null)
            .ToListAsync();

        foreach (var spool in spools)
            spool.FilamentMaterial = await _db.FilamentMaterials
                .FirstOrDefaultAsync(m => m.Id == spool.FilamentMaterialId);

        return spools;
    }
}

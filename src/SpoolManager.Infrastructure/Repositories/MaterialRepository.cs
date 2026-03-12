using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IMaterialRepository
{
    Task<List<FilamentMaterial>> GetAllAsync(Guid projectId, string? search = null);
    Task<List<FilamentMaterial>> GetGlobalAsync(string? search = null);
    Task<FilamentMaterial?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(FilamentMaterial material);
    Task UpdateAsync(FilamentMaterial material);
    Task DeleteAsync(Guid id);
    Task<List<FilamentMaterial>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task IncrementClickCountAsync(Guid id);
    Task<List<FilamentMaterial>> GetTopClickedGlobalAsync(int count);
}

public class MaterialRepository : IMaterialRepository
{
    private readonly SpoolManagerDb _db;

    public MaterialRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<FilamentMaterial>> GetAllAsync(Guid projectId, string? search = null)
    {
        var query = _db.FilamentMaterials.Where(m => m.ProjectId == projectId || m.ProjectId == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Brand.Contains(search) || m.Type.Contains(search));

        return await query.OrderBy(m => m.Brand).ThenBy(m => m.Type).ToListAsync();
    }

    public async Task<List<FilamentMaterial>> GetGlobalAsync(string? search = null)
    {
        var query = _db.FilamentMaterials.Where(m => m.ProjectId == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Brand.Contains(search) || m.Type.Contains(search));

        return await query.OrderBy(m => m.Brand).ThenBy(m => m.Type).ToListAsync();
    }

    public async Task<FilamentMaterial?> GetByIdAsync(Guid id) =>
        await _db.FilamentMaterials.FirstOrDefaultAsync(m => m.Id == id);

    public async Task<Guid> CreateAsync(FilamentMaterial material)
    {
        material.Id = Guid.NewGuid();
        material.CreatedAt = DateTime.UtcNow;
        material.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(material);
        return material.Id;
    }

    public async Task UpdateAsync(FilamentMaterial material) =>
        await _db.UpdateAsync(material);

    public async Task DeleteAsync(Guid id) =>
        await _db.FilamentMaterials.Where(m => m.Id == id).DeleteAsync();

    public async Task<List<FilamentMaterial>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await _db.FilamentMaterials.Where(m => idList.Contains(m.Id)).ToListAsync();
    }

    public async Task IncrementClickCountAsync(Guid id) =>
        await _db.FilamentMaterials
            .Where(m => m.Id == id)
            .Set(m => m.ReorderClickCount, m => m.ReorderClickCount + 1)
            .UpdateAsync();

    public async Task<List<FilamentMaterial>> GetTopClickedGlobalAsync(int count) =>
        await _db.FilamentMaterials
            .Where(m => m.ProjectId == null && m.ReorderUrl != null)
            .OrderByDescending(m => m.ReorderClickCount)
            .Take(count)
            .ToListAsync();
}

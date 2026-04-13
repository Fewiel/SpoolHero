using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IMaterialRepository
{
    Task<List<FilamentMaterial>> GetAllAsync(Guid projectId, string? search = null);
    Task<List<FilamentMaterial>> SearchAsync(Guid projectId, string query, int limit = 250);
    Task<int> CountAsync(Guid projectId);
    Task<(List<FilamentMaterial> Items, int TotalCount)> GetPagedAsync(Guid projectId, int page, int pageSize, string? type = null, string? brand = null, string? color = null);
    Task<List<string>> GetDistinctTypesAsync(Guid projectId);
    Task<List<string>> GetDistinctBrandsAsync(Guid projectId);
    Task<List<string>> GetDistinctColorsAsync(Guid projectId);
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

    public async Task<List<FilamentMaterial>> SearchAsync(Guid projectId, string query, int limit = 250)
    {
        var baseQuery = _db.FilamentMaterials.Where(m => m.ProjectId == projectId || m.ProjectId == null);

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var term in terms)
        {
            var t = term;
            baseQuery = baseQuery.Where(m =>
                m.Brand.Contains(t) || m.Type.Contains(t) || (m.ColorName != null && m.ColorName.Contains(t)));
        }

        return await baseQuery
            .OrderBy(m => m.Brand).ThenBy(m => m.Type)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> CountAsync(Guid projectId) =>
        await _db.FilamentMaterials.CountAsync(m => m.ProjectId == projectId || m.ProjectId == null);

    public async Task<(List<FilamentMaterial> Items, int TotalCount)> GetPagedAsync(Guid projectId, int page, int pageSize, string? type = null, string? brand = null, string? color = null)
    {
        var query = _db.FilamentMaterials.Where(m => m.ProjectId == projectId || m.ProjectId == null);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(m => m.Type == type);
        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(m => m.Brand.Contains(brand));
        if (!string.IsNullOrWhiteSpace(color))
            query = query.Where(m => m.ColorName != null && m.ColorName.Contains(color));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.Brand).ThenBy(m => m.Type)
            .Skip(page * pageSize).Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<string>> GetDistinctTypesAsync(Guid projectId) =>
        await _db.FilamentMaterials
            .Where(m => m.ProjectId == projectId || m.ProjectId == null)
            .Select(m => m.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    public async Task<List<string>> GetDistinctBrandsAsync(Guid projectId) =>
        await _db.FilamentMaterials
            .Where(m => m.ProjectId == projectId || m.ProjectId == null)
            .Select(m => m.Brand)
            .Where(b => b != "")
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

    public async Task<List<string>> GetDistinctColorsAsync(Guid projectId) =>
        await _db.FilamentMaterials
            .Where(m => m.ProjectId == projectId || m.ProjectId == null)
            .Where(m => m.ColorName != null && m.ColorName != "")
            .Select(m => m.ColorName!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    public async Task<List<FilamentMaterial>> GetGlobalAsync(string? search = null)
    {
        var query = _db.FilamentMaterials.Where(m => m.ProjectId == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Brand.Contains(search) || m.Type.Contains(search) || (m.ColorName != null && m.ColorName.Contains(search)));

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

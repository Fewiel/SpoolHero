using LinqToDB;
using LinqToDB.Data;
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
    Task<(List<FilamentMaterial> Items, int TotalCount, List<string> Brands, List<string> Types)> SearchAsync(
        string? query, string? materialType, string? brand, bool? globalOnly, int limit, int offset, Guid? projectId);
    Task<FilamentMaterial?> GetByExternalIdAsync(string externalId);
    Task BulkUpsertSyncedAsync(List<FilamentMaterial> materials);
    Task<List<string>> GetDistinctBrandsAsync(Guid? projectId);
    Task<List<string>> GetDistinctTypesAsync(Guid? projectId);
    Task<int> GetSyncedCountAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
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

    public async Task<(List<FilamentMaterial> Items, int TotalCount, List<string> Brands, List<string> Types)> SearchAsync(
        string? query, string? materialType, string? brand, bool? globalOnly, int limit, int offset, Guid? projectId)
    {
        var q = _db.FilamentMaterials.AsQueryable();

        if (globalOnly == true)
            q = q.Where(m => m.ProjectId == null);
        else if (projectId != null)
            q = q.Where(m => m.ProjectId == projectId || m.ProjectId == null);
        else
            q = q.Where(m => m.ProjectId == null);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query;
            q = q.Where(m =>
                m.Brand.Contains(search) ||
                m.Type.Contains(search) ||
                (m.ColorName != null && m.ColorName.Contains(search)) ||
                (m.ProductName != null && m.ProductName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(materialType))
            q = q.Where(m => m.Type == materialType);

        if (!string.IsNullOrWhiteSpace(brand))
            q = q.Where(m => m.Brand == brand);

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderBy(m => m.Brand)
            .ThenBy(m => m.Type)
            .ThenBy(m => m.ColorName)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var baseQuery = _db.FilamentMaterials.AsQueryable();
        if (projectId != null)
            baseQuery = baseQuery.Where(m => m.ProjectId == projectId || m.ProjectId == null);
        else
            baseQuery = baseQuery.Where(m => m.ProjectId == null);

        var brands = await baseQuery.Select(m => m.Brand).Distinct().OrderBy(b => b).ToListAsync();
        var types = await baseQuery.Select(m => m.Type).Distinct().OrderBy(t => t).ToListAsync();

        return (items, totalCount, brands, types);
    }

    public async Task<FilamentMaterial?> GetByExternalIdAsync(string externalId) =>
        await _db.FilamentMaterials.FirstOrDefaultAsync(m => m.ExternalId == externalId);

    public async Task BulkUpsertSyncedAsync(List<FilamentMaterial> materials)
    {
        var externalIds = materials.Where(m => m.ExternalId != null).Select(m => m.ExternalId!).ToList();
        var existingMaterials = new List<FilamentMaterial>();
        foreach (var chunk in externalIds.Chunk(500))
        {
            var chunkList = chunk.ToList();
            var batch = await _db.FilamentMaterials
                .Where(m => m.ExternalId != null && chunkList.Contains(m.ExternalId))
                .ToListAsync();
            existingMaterials.AddRange(batch);
        }
        var existingByExtId = existingMaterials.ToDictionary(m => m.ExternalId!);

        var toInsert = new List<FilamentMaterial>();
        var toUpdate = new List<FilamentMaterial>();

        foreach (var material in materials)
        {
            if (material.ExternalId != null && existingByExtId.TryGetValue(material.ExternalId, out var existing))
            {
                material.Id = existing.Id;
                material.CreatedAt = existing.CreatedAt;
                material.UpdatedAt = DateTime.UtcNow;
                toUpdate.Add(material);
            }
            else
            {
                material.Id = Guid.NewGuid();
                material.CreatedAt = DateTime.UtcNow;
                material.UpdatedAt = DateTime.UtcNow;
                toInsert.Add(material);
            }
        }

        await using var transaction = await _db.BeginTransactionAsync();

        if (toInsert.Count > 0)
        {
            foreach (var batch in toInsert.Chunk(100))
            {
                foreach (var m in batch)
                    await _db.InsertAsync(m);
            }
        }

        foreach (var m in toUpdate)
            await _db.UpdateAsync(m);

        await transaction.CommitAsync();
    }

    public async Task<List<string>> GetDistinctBrandsAsync(Guid? projectId)
    {
        var query = _db.FilamentMaterials.AsQueryable();
        if (projectId != null)
            query = query.Where(m => m.ProjectId == projectId || m.ProjectId == null);
        else
            query = query.Where(m => m.ProjectId == null);

        return await query.Select(m => m.Brand).Distinct().OrderBy(b => b).ToListAsync();
    }

    public async Task<List<string>> GetDistinctTypesAsync(Guid? projectId)
    {
        var query = _db.FilamentMaterials.AsQueryable();
        if (projectId != null)
            query = query.Where(m => m.ProjectId == projectId || m.ProjectId == null);
        else
            query = query.Where(m => m.ProjectId == null);

        return await query.Select(m => m.Type).Distinct().OrderBy(t => t).ToListAsync();
    }

    public async Task<int> GetSyncedCountAsync() =>
        await _db.FilamentMaterials.Where(m => m.Source == "spoolmandb").CountAsync();

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        var latest = await _db.FilamentMaterials
            .Where(m => m.Source == "spoolmandb")
            .OrderByDescending(m => m.UpdatedAt)
            .FirstOrDefaultAsync();
        return latest?.UpdatedAt;
    }
}

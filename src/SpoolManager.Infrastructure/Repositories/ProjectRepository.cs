using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IProjectRepository
{
    Task<List<Project>> GetForUserAsync(Guid userId);
    Task<Project?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
    Task<int> GetTotalCountAsync();
    Task<int> GetWithSpoolsCountAsync();
}

public class ProjectRepository : IProjectRepository
{
    private readonly SpoolManagerDb _db;

    public ProjectRepository(SpoolManagerDb db) => _db = db;

    public async Task<List<Project>> GetForUserAsync(Guid userId)
    {
        var projectIds = await _db.ProjectMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ProjectId)
            .ToListAsync();
        return await _db.Projects.Where(p => projectIds.Contains(p.Id)).OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Project?> GetByIdAsync(Guid id) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Guid> CreateAsync(Project project)
    {
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(project);
        return project.Id;
    }

    public async Task UpdateAsync(Project project) =>
        await _db.UpdateAsync(project);

    // Webhooks are removed by the ON DELETE CASCADE foreign key (M020).
    public async Task DeleteAsync(Guid id)
    {
        await using var tx = await _db.BeginTransactionAsync();

        await _db.Spools.Where(s => s.ProjectId == id).DeleteAsync();
        await _db.Printers.Where(p => p.ProjectId == id).DeleteAsync();
        await _db.Dryers.Where(d => d.ProjectId == id).DeleteAsync();
        await _db.StorageLocations.Where(sl => sl.ProjectId == id).DeleteAsync();
        await _db.Invitations.Where(i => i.ProjectId == id).DeleteAsync();
        await _db.ProjectMembers.Where(m => m.ProjectId == id).DeleteAsync();

        // Public materials of this project that other projects still use are kept as global materials.
        await _db.FilamentMaterials
            .Where(m => m.ProjectId == id && _db.Spools.Any(s => s.FilamentMaterialId == m.Id))
            .Set(m => m.ProjectId, (Guid?)null)
            .UpdateAsync();
        var materialIds = _db.FilamentMaterials.Where(m => m.ProjectId == id).Select(m => m.Id);
        await _db.UserMaterialFavorites.Where(f => materialIds.Contains(f.MaterialId)).DeleteAsync();
        await _db.FilamentMaterials.Where(m => m.ProjectId == id).DeleteAsync();

        // Spoolman API keys and webhooks are removed by ON DELETE CASCADE.
        await _db.Projects.Where(p => p.Id == id).DeleteAsync();

        await tx.CommitAsync();
    }

    public async Task<int> GetTotalCountAsync() =>
        await _db.Projects.CountAsync();

    public async Task<int> GetWithSpoolsCountAsync() =>
        await _db.Projects
            .Where(p => _db.Spools.Any(s => s.ProjectId == p.Id))
            .CountAsync();
}

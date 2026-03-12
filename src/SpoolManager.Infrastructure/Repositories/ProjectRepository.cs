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

    public async Task DeleteAsync(Guid id) =>
        await _db.Projects.Where(p => p.Id == id).DeleteAsync();

    public async Task<int> GetTotalCountAsync() =>
        await _db.Projects.CountAsync();

    public async Task<int> GetWithSpoolsCountAsync() =>
        await _db.Projects
            .Where(p => _db.Spools.Any(s => s.ProjectId == p.Id))
            .CountAsync();
}

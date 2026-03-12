using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IProjectMemberRepository
{
    Task<List<ProjectMember>> GetByProjectAsync(Guid projectId);
    Task<ProjectMember?> GetAsync(Guid projectId, Guid userId);
    Task AddAsync(ProjectMember member);
    Task UpdateAsync(ProjectMember member);
    Task RemoveAsync(Guid projectId, Guid userId);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId);
}

public class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly SpoolManagerDb _db;

    public ProjectMemberRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<ProjectMember>> GetByProjectAsync(Guid projectId)
    {
        var members = await _db.ProjectMembers.Where(m => m.ProjectId == projectId).ToListAsync();
        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id);
        foreach (var m in members)
        {
            if (userMap.TryGetValue(m.UserId, out var u))
            {
                m.Username = u.Username;
                m.Email = u.Email;
            }
        }
        return members;
    }

    public async Task<ProjectMember?> GetAsync(Guid projectId, Guid userId) =>
        await _db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

    public async Task AddAsync(ProjectMember member)
    {
        member.Id = Guid.NewGuid();
        member.JoinedAt = DateTime.UtcNow;
        await _db.InsertAsync(member);
    }

    public async Task UpdateAsync(ProjectMember member) =>
        await _db.UpdateAsync(member);

    public async Task RemoveAsync(Guid projectId, Guid userId) =>
        await _db.ProjectMembers.Where(m => m.ProjectId == projectId && m.UserId == userId).DeleteAsync();

    public async Task<bool> IsMemberAsync(Guid projectId, Guid userId) =>
        await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
}

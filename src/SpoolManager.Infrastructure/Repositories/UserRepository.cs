using LinqToDB;
using SpoolManager.Infrastructure.Data;

namespace SpoolManager.Infrastructure.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email);
    Task<AppUser?> GetByIdAsync(Guid id);
    Task<AppUser?> GetByVerificationTokenAsync(string token);
    Task<List<AppUser>> GetAllAsync();
    Task<List<AppUser>> GetAdminsAsync();
    Task<List<AppUser>> GetProjectMembersAsync(Guid projectId);
    Task<bool> ExistsAsync(string email, string username);
    Task CreateAsync(AppUser user);
    Task UpdateAsync(AppUser user);
    Task DeleteAsync(Guid id);
    Task<int> GetCountAsync();
    Task UpdateLastActiveAsync(Guid userId, DateTime timestamp);
    Task SetAdminAsync(Guid userId, bool isAdmin, int newTokenVersion);
}

public class UserRepository : IUserRepository
{
    private readonly SpoolManagerDb _db;

    public UserRepository(SpoolManagerDb db) => _db = db;

    public async Task<AppUser?> GetByEmailAsync(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<AppUser?> GetByIdAsync(Guid id) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<AppUser?> GetByVerificationTokenAsync(string token) =>
        await _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

    public async Task<List<AppUser>> GetAllAsync() =>
        await _db.Users.OrderBy(u => u.Username).ToListAsync();

    public async Task<List<AppUser>> GetAdminsAsync() =>
        await _db.Users.Where(u => u.IsPlatformAdmin).ToListAsync();

    public async Task<List<AppUser>> GetProjectMembersAsync(Guid projectId)
    {
        var memberUserIds = await _db.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => pm.UserId)
            .ToListAsync();
        return await _db.Users
            .Where(u => memberUserIds.Contains(u.Id))
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string email, string username) =>
        await _db.Users.AnyAsync(u => u.Email == email || u.Username == username);

    public async Task CreateAsync(AppUser user)
    {
        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTime.UtcNow;
        await _db.InsertAsync(user);
    }

    public async Task UpdateAsync(AppUser user) =>
        await _db.UpdateAsync(user);

    public async Task DeleteAsync(Guid id)
    {
        var ticketIds = await _db.Tickets
            .Where(t => t.UserId == id)
            .Select(t => t.Id)
            .ToListAsync();

        if (ticketIds.Count > 0)
            await _db.TicketComments
                .Where(c => ticketIds.Contains(c.TicketId))
                .DeleteAsync();

        await _db.TicketComments.Where(c => c.UserId == id).DeleteAsync();
        await _db.Tickets.Where(t => t.UserId == id).DeleteAsync();

        await _db.Tickets
            .Where(t => t.AssignedToUserId == id)
            .Set(t => t.AssignedToUserId, (Guid?)null)
            .Set(t => t.AssignedToUsername, (string?)null)
            .UpdateAsync();

        await _db.ProjectMembers.Where(pm => pm.UserId == id).DeleteAsync();
        await _db.Invitations.Where(i => i.InvitedByUserId == id).DeleteAsync();

        await _db.Invitations
            .Where(i => i.UsedByUserId == id)
            .Set(i => i.UsedByUserId, (Guid?)null)
            .UpdateAsync();

        await _db.AuditLogs
            .Where(a => a.UserId == id)
            .Set(a => a.UserId, (Guid?)null)
            .UpdateAsync();

        await _db.Users.Where(u => u.Id == id).DeleteAsync();
    }

    public async Task<int> GetCountAsync() =>
        await _db.Users.CountAsync();

    public async Task UpdateLastActiveAsync(Guid userId, DateTime timestamp) =>
        await _db.Users
            .Where(u => u.Id == userId)
            .Set(u => u.LastActiveAt, timestamp)
            .UpdateAsync();

    public async Task SetAdminAsync(Guid userId, bool isAdmin, int newTokenVersion) =>
        await _db.Users
            .Where(u => u.Id == userId)
            .Set(u => u.IsPlatformAdmin, isAdmin)
            .Set(u => u.TokenVersion, newTokenVersion)
            .UpdateAsync();
}

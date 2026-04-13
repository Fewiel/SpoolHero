using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> GetByTokenAsync(string token);
    Task<List<Invitation>> GetByProjectAsync(Guid projectId);
    Task<Guid> CreateAsync(Invitation invitation);
    Task MarkUsedAsync(Guid id, Guid usedByUserId);
}

public class InvitationRepository : IInvitationRepository
{
    private readonly SpoolManagerDb _db;

    public InvitationRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<Invitation?> GetByTokenAsync(string token)
    {
        var inv = await _db.Invitations.FirstOrDefaultAsync(i => i.Token == token);
        if (inv == null)
            return null;
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == inv.ProjectId);
        var inviter = await _db.Users.FirstOrDefaultAsync(u => u.Id == inv.InvitedByUserId);
        inv.ProjectName = project?.Name;
        inv.InvitedByUsername = inviter?.Username;
        return inv;
    }

    public async Task<List<Invitation>> GetByProjectAsync(Guid projectId) =>
        await _db.Invitations.Where(i => i.ProjectId == projectId).OrderByDescending(i => i.CreatedAt).ToListAsync();

    public async Task<Guid> CreateAsync(Invitation invitation)
    {
        invitation.Id = Guid.NewGuid();
        invitation.CreatedAt = DateTime.UtcNow;
        await _db.InsertAsync(invitation);
        return invitation.Id;
    }

    public async Task MarkUsedAsync(Guid id, Guid usedByUserId) =>
        await _db.Invitations.Where(i => i.Id == id).Set(i => i.UsedByUserId, usedByUserId).Set(i => i.UsedAt, DateTime.UtcNow).UpdateAsync();
}

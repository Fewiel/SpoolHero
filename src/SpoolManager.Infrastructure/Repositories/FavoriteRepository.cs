using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IFavoriteRepository
{
    Task<List<Guid>> GetMaterialIdsByUserAsync(Guid userId);
    Task AddAsync(Guid userId, Guid materialId);
    Task RemoveAsync(Guid userId, Guid materialId);
}

public class FavoriteRepository : IFavoriteRepository
{
    private readonly SpoolManagerDb _db;

    public FavoriteRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<Guid>> GetMaterialIdsByUserAsync(Guid userId) =>
        await _db.UserMaterialFavorites
            .Where(f => f.UserId == userId)
            .Select(f => f.MaterialId)
            .ToListAsync();

    public async Task AddAsync(Guid userId, Guid materialId)
    {
        var exists = await _db.UserMaterialFavorites
            .AnyAsync(f => f.UserId == userId && f.MaterialId == materialId);

        if (exists)
            return;

        await _db.InsertAsync(new UserMaterialFavorite
        {
            UserId = userId,
            MaterialId = materialId,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task RemoveAsync(Guid userId, Guid materialId) =>
        await _db.UserMaterialFavorites
            .Where(f => f.UserId == userId && f.MaterialId == materialId)
            .DeleteAsync();
}

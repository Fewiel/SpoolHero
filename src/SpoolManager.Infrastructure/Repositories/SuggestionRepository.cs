using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface ISuggestionRepository
{
    Task<List<MaterialSuggestion>> GetAllAsync(string? status = null);
    Task<List<MaterialSuggestion>> GetByUserAsync(Guid userId);
    Task<MaterialSuggestion?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(MaterialSuggestion suggestion);
    Task UpdateAsync(MaterialSuggestion suggestion);
    Task<int> CountPendingAsync();
}

public class SuggestionRepository : ISuggestionRepository
{
    private readonly SpoolManagerDb _db;
    public SuggestionRepository(SpoolManagerDb db) => _db = db;

    public async Task<List<MaterialSuggestion>> GetAllAsync(string? status = null)
    {
        var query = _db.MaterialSuggestions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);
        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<List<MaterialSuggestion>> GetByUserAsync(Guid userId)
    {
        return await _db.MaterialSuggestions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<MaterialSuggestion?> GetByIdAsync(Guid id)
    {
        return await _db.MaterialSuggestions.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Guid> CreateAsync(MaterialSuggestion suggestion)
    {
        suggestion.Id = Guid.NewGuid();
        suggestion.CreatedAt = DateTime.UtcNow;
        await _db.InsertAsync(suggestion);
        return suggestion.Id;
    }

    public async Task UpdateAsync(MaterialSuggestion suggestion)
    {
        await _db.UpdateAsync(suggestion);
    }

    public async Task<int> CountPendingAsync()
    {
        return await _db.MaterialSuggestions.CountAsync(s => s.Status == MaterialSuggestion.StatusPending);
    }
}

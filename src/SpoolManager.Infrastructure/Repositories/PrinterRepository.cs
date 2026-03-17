using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IPrinterRepository
{
    Task<List<Printer>> GetAllAsync(Guid projectId);
    Task<List<Printer>> GetAllForInventoryAsync();
    Task<Printer?> GetByIdAsync(Guid id);
    Task<List<Printer>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<Guid> CreateAsync(Printer printer);
    Task UpdateAsync(Printer printer);
    Task DeleteAsync(Guid id);
}

public class PrinterRepository : IPrinterRepository
{
    private readonly SpoolManagerDb _db;

    public PrinterRepository(SpoolManagerDb db)
    {
        _db = db;
    }

    public async Task<List<Printer>> GetAllAsync(Guid projectId) =>
        await _db.Printers.Where(p => p.ProjectId == projectId).OrderBy(p => p.Name).ToListAsync();

    public async Task<List<Printer>> GetAllForInventoryAsync() =>
        await _db.Printers.OrderBy(p => p.Name).ToListAsync();

    public async Task<Printer?> GetByIdAsync(Guid id) =>
        await _db.Printers.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Printer>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await _db.Printers.Where(p => idList.Contains(p.Id)).ToListAsync();
    }

    public async Task<Guid> CreateAsync(Printer printer)
    {
        printer.Id = Guid.NewGuid();
        printer.CreatedAt = DateTime.UtcNow;
        printer.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(printer);
        return printer.Id;
    }

    public async Task UpdateAsync(Printer printer) =>
        await _db.UpdateAsync(printer);

    public async Task DeleteAsync(Guid id) =>
        await _db.Printers.Where(p => p.Id == id).DeleteAsync();
}

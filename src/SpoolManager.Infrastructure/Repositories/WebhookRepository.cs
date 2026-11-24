using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Repositories;

public interface IWebhookRepository
{
    Task<List<Webhook>> GetAllAsync(Guid projectId);
    Task<List<Webhook>> GetEnabledForEventAsync(Guid projectId, string eventType);
    Task<Webhook?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Webhook webhook);
    Task UpdateAsync(Webhook webhook);
    Task UpdateDeliveryStatusAsync(Guid id, DateTime at, bool success, int? statusCode, string? error);
    Task DeleteAsync(Guid id);
    Task DeleteByProjectAsync(Guid projectId);
}

public class WebhookRepository : IWebhookRepository
{
    private readonly SpoolManagerDb _db;

    public WebhookRepository(SpoolManagerDb db) => _db = db;

    public async Task<List<Webhook>> GetAllAsync(Guid projectId) =>
        await _db.Webhooks.Where(w => w.ProjectId == projectId).OrderBy(w => w.Name).ToListAsync();

    public async Task<List<Webhook>> GetEnabledForEventAsync(Guid projectId, string eventType)
    {
        var query = _db.Webhooks.Where(w => w.ProjectId == projectId && w.IsEnabled);
        query = eventType switch
        {
            WebhookEvents.DryerFinished => query.Where(w => w.OnDryerFinished),
            WebhookEvents.SpoolLow => query.Where(w => w.OnSpoolLow),
            WebhookEvents.SpoolConsumed => query.Where(w => w.OnSpoolConsumed),
            _ => query.Where(w => false)
        };
        return await query.ToListAsync();
    }

    public async Task<Webhook?> GetByIdAsync(Guid id) =>
        await _db.Webhooks.FirstOrDefaultAsync(w => w.Id == id);

    public async Task<Guid> CreateAsync(Webhook webhook)
    {
        webhook.Id = Guid.NewGuid();
        webhook.CreatedAt = DateTime.UtcNow;
        webhook.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(webhook);
        return webhook.Id;
    }

    public async Task UpdateAsync(Webhook webhook) =>
        await _db.UpdateAsync(webhook);

    public async Task UpdateDeliveryStatusAsync(Guid id, DateTime at, bool success, int? statusCode, string? error)
    {
        if (error != null && error.Length > 1000)
            error = error[..1000];
        await _db.Webhooks
            .Where(w => w.Id == id)
            .Set(w => w.LastTriggeredAt, at)
            .Set(w => w.LastSuccess, success)
            .Set(w => w.LastStatusCode, statusCode)
            .Set(w => w.LastError, error)
            .UpdateAsync();
    }

    public async Task DeleteAsync(Guid id) =>
        await _db.Webhooks.Where(w => w.Id == id).DeleteAsync();

    public async Task DeleteByProjectAsync(Guid projectId) =>
        await _db.Webhooks.Where(w => w.ProjectId == projectId).DeleteAsync();
}

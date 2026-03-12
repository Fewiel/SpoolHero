using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services;

public interface IAuditService
{
    Task LogAsync(string action,
        Guid? userId = null, string? username = null,
        string? entityType = null, string? entityId = null, string? entityName = null,
        Guid? projectId = null, string? projectName = null,
        string? details = null, string? ipAddress = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repo;

    public AuditService(IAuditLogRepository repo) => _repo = repo;

    public async Task LogAsync(string action,
        Guid? userId = null, string? username = null,
        string? entityType = null, string? entityId = null, string? entityName = null,
        Guid? projectId = null, string? projectName = null,
        string? details = null, string? ipAddress = null)
    {
        try
        {
            var log = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                UserId = userId,
                Username = username,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                ProjectId = projectId,
                ProjectName = projectName,
                Details = details,
                IpAddress = ipAddress
            };
            await _repo.CreateAsync(log);
        }
        catch { }
    }
}

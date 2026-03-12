namespace SpoolManager.Shared.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

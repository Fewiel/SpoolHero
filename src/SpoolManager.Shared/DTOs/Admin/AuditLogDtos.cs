namespace SpoolManager.Shared.DTOs.Admin;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
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

public class AuditLogPageResponse
{
    public List<AuditLogDto> Logs { get; set; } = [];
    public int Total { get; set; }
}

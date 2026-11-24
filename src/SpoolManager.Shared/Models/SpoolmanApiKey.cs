namespace SpoolManager.Shared.Models;

public class SpoolmanApiKey
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

namespace SpoolManager.Shared.Models;

public class Dryer
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public bool IsDrying { get; set; }
    public DateTime? DryingStartedAt { get; set; }
    public DateTime? DryingFinishAt { get; set; }
    public DateTime? DryingNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

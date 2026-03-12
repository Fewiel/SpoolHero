namespace SpoolManager.Shared.Models;

public class Printer
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RfidTagUid { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

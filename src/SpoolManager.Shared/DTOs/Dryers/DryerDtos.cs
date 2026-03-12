namespace SpoolManager.Shared.DTOs.Dryers;

public class DryerDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateDryerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
}

public class UpdateDryerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
}

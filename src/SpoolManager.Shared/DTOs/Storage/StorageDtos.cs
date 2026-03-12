namespace SpoolManager.Shared.DTOs.Storage;

public class StorageLocationDto
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

public class CreateStorageLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
}

public class UpdateStorageLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTagUid { get; set; }
}

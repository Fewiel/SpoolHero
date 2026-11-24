namespace SpoolManager.Shared.DTOs.Tags;

public class TagEncodeRequest
{
    public Guid? SpoolId { get; set; }
    public Guid? MaterialId { get; set; }
}

public class TagEncodeResponse
{
    public string Base64 { get; set; } = string.Empty;
    public string? JsonPayload { get; set; }
    public int? SpoolmanId { get; set; }
}

public class TagDecodeRequest
{
    public string Base64 { get; set; } = string.Empty;
}

public class TagDecodeResponse
{
    public string? Type { get; set; }
    public string? ColorHex { get; set; }
    public string? Brand { get; set; }
    public int? MinTemp { get; set; }
    public int? MaxTemp { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public Guid? SpoolId { get; set; }
}

public class TagEncodeEntityRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
}

public class InventoryIdentifyRequest
{
    public string? JsonPayload { get; set; }
    public string? SerialNumber { get; set; }
}

public class InventoryIdentifyResult
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
}

public class InventoryActionRequest
{
    public InventoryIdentifyResult First { get; set; } = new();
    public InventoryIdentifyResult Second { get; set; } = new();
}

public class InventoryActionResult
{
    public bool Success { get; set; }
    public string Description { get; set; } = string.Empty;
}

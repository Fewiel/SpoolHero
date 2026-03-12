namespace SpoolManager.Shared.DTOs.Printers;

public class PrinterDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RfidTagUid { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePrinterRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RfidTagUid { get; set; }
}

public class UpdatePrinterRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RfidTagUid { get; set; }
}

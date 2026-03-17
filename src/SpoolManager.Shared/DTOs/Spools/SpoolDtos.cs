namespace SpoolManager.Shared.DTOs.Spools;

public class SpoolDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FilamentMaterialId { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string MaterialBrand { get; set; } = string.Empty;
    public string MaterialColorHex { get; set; } = "FFFFFF";
    public string? MaterialColorName { get; set; }
    public string? RfidTagUid { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? RepackagedAt { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public DateTime? DriedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public decimal RemainingWeightGrams { get; set; }
    public decimal RemainingPercent { get; set; }
    public Guid? PrinterId { get; set; }
    public string? PrinterName { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string? StorageLocationName { get; set; }
    public Guid? DryerId { get; set; }
    public string? DryerName { get; set; }
    public int? MaterialDryTimeHours { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? ReorderUrl { get; set; }
    public string? Notes { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateSpoolRequest
{
    public Guid FilamentMaterialId { get; set; }
    public string? RfidTagUid { get; set; }
    public decimal RemainingWeightGrams { get; set; }
    public decimal RemainingPercent { get; set; } = 100;
    public Guid? PrinterId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public Guid? DryerId { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? ReorderUrl { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSpoolRequest
{
    public Guid FilamentMaterialId { get; set; }
    public decimal RemainingWeightGrams { get; set; }
    public decimal RemainingPercent { get; set; }
    public string? RfidTagUid { get; set; }
    public Guid? PrinterId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public Guid? DryerId { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? ReorderUrl { get; set; }
    public string? Notes { get; set; }
}

public class UpdateRemainingRequest
{
    public decimal RemainingWeightGrams { get; set; }
    public decimal RemainingPercent { get; set; }
}

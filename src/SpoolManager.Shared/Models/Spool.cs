namespace SpoolManager.Shared.Models;

public class Spool
{
    public Guid Id { get; set; }
    public int SpoolmanId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FilamentMaterialId { get; set; }
    public FilamentMaterial? FilamentMaterial { get; set; }
    public string? RfidTagUid { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? RepackagedAt { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public DateTime? DriedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public decimal RemainingWeightGrams { get; set; }
    public decimal RemainingPercent { get; set; }
    public Guid? PrinterId { get; set; }
    public Printer? Printer { get; set; }
    public Guid? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public Guid? DryerId { get; set; }
    public Dryer? Dryer { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? ReorderUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime? LowSpoolNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

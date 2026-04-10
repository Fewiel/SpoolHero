namespace SpoolManager.Shared.Models;

public class MaterialSuggestion
{
    public const string StatusPending = "pending";
    public const string StatusApproved = "approved";
    public const string StatusRejected = "rejected";

    public Guid Id { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "FFFFFF";
    public string? ColorName { get; set; }
    public int MinTempCelsius { get; set; }
    public int MaxTempCelsius { get; set; }
    public int? BedTempCelsius { get; set; }
    public decimal DiameterMm { get; set; } = 1.75m;
    public decimal? DensityGCm3 { get; set; }
    public int? DryTempCelsius { get; set; }
    public int? DryTimeHours { get; set; }
    public string? Notes { get; set; }
    public string? ReorderUrl { get; set; }
    public decimal? PricePerKg { get; set; }
    public string Status { get; set; } = StatusPending;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}

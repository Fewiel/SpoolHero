namespace SpoolManager.Shared.Models;

public class FilamentMaterial
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "FFFFFF";
    public string Brand { get; set; } = string.Empty;
    public int MinTempCelsius { get; set; }
    public int MaxTempCelsius { get; set; }
    public string? ColorName { get; set; }
    public decimal DiameterMm { get; set; } = 1.75m;
    public int? WeightGrams { get; set; }
    public int? BedTempCelsius { get; set; }
    public decimal? DensityGCm3 { get; set; }
    public int? DryTempCelsius { get; set; }
    public int? DryTimeHours { get; set; }
    public string? Notes { get; set; }
    public string? ReorderUrl { get; set; }
    public decimal? PricePerKg { get; set; }
    public bool IsPublic { get; set; }
    public int ReorderClickCount { get; set; }
    public string? OfdFilamentId { get; set; }
    public string? OfdVariantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

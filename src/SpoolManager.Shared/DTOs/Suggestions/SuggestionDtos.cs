namespace SpoolManager.Shared.DTOs.Suggestions;

public class MaterialSuggestionDto
{
    public Guid Id { get; set; }
    public Guid? MaterialId { get; set; }
    public string? MaterialName { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class CreateSuggestionRequest
{
    public Guid? MaterialId { get; set; }
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
}

public class ReviewSuggestionRequest
{
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}

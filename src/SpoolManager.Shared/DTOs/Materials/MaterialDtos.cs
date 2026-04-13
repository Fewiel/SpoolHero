namespace SpoolManager.Shared.DTOs.Materials;

public class FilamentMaterialDto
{
    public Guid Id { get; set; }
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

public class MaterialSummaryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "FFFFFF";
    public string? ColorName { get; set; }
    public int MinTempCelsius { get; set; }
    public int MaxTempCelsius { get; set; }
    public decimal DiameterMm { get; set; }
    public string? ReorderUrl { get; set; }
    public string? OfdVariantId { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public List<string> Types { get; set; } = [];
    public List<string> Brands { get; set; } = [];
    public List<string> Colors { get; set; } = [];
}

public class CreateMaterialRequest
{
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
}

public class UpdateMaterialRequest : CreateMaterialRequest { }

public class MaterialExportPayload
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; }
    public List<FilamentMaterialDto> Materials { get; set; } = [];
}

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
    public string Source { get; set; } = "manual";
    public string? ProductName { get; set; }
    public int? SpoolWeightGrams { get; set; }
    public string? SpoolType { get; set; }
    public string? Finish { get; set; }
    public bool Translucent { get; set; }
    public bool Glow { get; set; }
    public string? Fill { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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

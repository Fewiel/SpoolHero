namespace SpoolManager.Shared.DTOs.Import;

public class CsvImportRow
{
    public string? Brand { get; set; }
    public string? Type { get; set; }
    public string? ColorName { get; set; }
    public string? ColorHex { get; set; }
    public int? MinTempCelsius { get; set; }
    public int? MaxTempCelsius { get; set; }
    public int? BedTempCelsius { get; set; }
    public decimal? DiameterMm { get; set; }
    public int? WeightGrams { get; set; }
    public decimal? DensityGCm3 { get; set; }
    public int? DryTempCelsius { get; set; }
    public int? DryTimeHours { get; set; }
    public decimal? PricePerKg { get; set; }
    public string? ReorderUrl { get; set; }
    public string? Notes { get; set; }

    public decimal? RemainingWeightGrams { get; set; }
    public decimal? RemainingPercent { get; set; }
    public decimal? PurchasePrice { get; set; }
}

public class CsvImportRequest
{
    public List<CsvImportRow> Rows { get; set; } = [];
    public bool CreateSpools { get; set; }
}

public class CsvImportResult
{
    public int MaterialsCreated { get; set; }
    public int SpoolsCreated { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = [];
}

public static class CsvImportFields
{
    public const string Ignore = "__ignore__";
    public const string Brand = "brand";
    public const string Type = "type";
    public const string ColorName = "color_name";
    public const string ColorHex = "color_hex";
    public const string MinTemp = "min_temp";
    public const string MaxTemp = "max_temp";
    public const string BedTemp = "bed_temp";
    public const string Diameter = "diameter";
    public const string Weight = "weight";
    public const string Density = "density";
    public const string DryTemp = "dry_temp";
    public const string DryTime = "dry_time";
    public const string PricePerKg = "price_per_kg";
    public const string ReorderUrl = "reorder_url";
    public const string Notes = "notes";
    public const string RemainingWeight = "remaining_weight";
    public const string RemainingPercent = "remaining_percent";
    public const string PurchasePrice = "purchase_price";

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [Ignore] = "-- ignore --",
        [Brand] = "Brand",
        [Type] = "Material Type",
        [ColorName] = "Color Name",
        [ColorHex] = "Color Hex",
        [MinTemp] = "Min Temp (°C)",
        [MaxTemp] = "Max Temp (°C)",
        [BedTemp] = "Bed Temp (°C)",
        [Diameter] = "Diameter (mm)",
        [Weight] = "Weight (g)",
        [Density] = "Density (g/cm³)",
        [DryTemp] = "Dry Temp (°C)",
        [DryTime] = "Dry Time (h)",
        [PricePerKg] = "Price/kg",
        [ReorderUrl] = "Reorder URL",
        [Notes] = "Notes",
        [RemainingWeight] = "Remaining Weight (g)",
        [RemainingPercent] = "Remaining %",
        [PurchasePrice] = "Purchase Price"
    };

    public static readonly Dictionary<string, string> AutoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["brand"] = Brand, ["manufacturer"] = Brand, ["hersteller"] = Brand,
        ["material"] = Type, ["type"] = Type, ["typ"] = Type, ["filament_type"] = Type,
        ["color"] = ColorName, ["colour"] = ColorName, ["farbe"] = ColorName, ["color_name"] = ColorName,
        ["hex"] = ColorHex, ["color_hex"] = ColorHex, ["farbcode"] = ColorHex, ["color_code"] = ColorHex,
        ["min_temp"] = MinTemp, ["nozzle_min"] = MinTemp, ["min_temperature"] = MinTemp,
        ["max_temp"] = MaxTemp, ["nozzle_max"] = MaxTemp, ["max_temperature"] = MaxTemp,
        ["bed_temp"] = BedTemp, ["bed_temperature"] = BedTemp,
        ["diameter"] = Diameter, ["diameter_mm"] = Diameter,
        ["weight"] = Weight, ["weight_grams"] = Weight, ["gewicht"] = Weight,
        ["density"] = Density, ["density_g_cm3"] = Density,
        ["dry_temp"] = DryTemp,
        ["dry_time"] = DryTime,
        ["price"] = PricePerKg, ["preis"] = PricePerKg, ["cost"] = PricePerKg, ["price_per_kg"] = PricePerKg,
        ["url"] = ReorderUrl, ["reorder_url"] = ReorderUrl, ["shop_url"] = ReorderUrl,
        ["notes"] = Notes, ["notizen"] = Notes,
        ["remaining"] = RemainingWeight, ["remaining_weight"] = RemainingWeight, ["rest"] = RemainingWeight,
        ["remaining_percent"] = RemainingPercent,
        ["purchase_price"] = PurchasePrice
    };
}

namespace SpoolManager.Shared.Models;

public class SlicerProfile
{
    public Guid Id { get; set; }
    public Guid FilamentMaterialId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PrinterId { get; set; }
    public Printer? Printer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SlicerType { get; set; } = "generic";

    public int? NozzleTemp { get; set; }
    public int? NozzleTempInitialLayer { get; set; }
    public int? BedTemp { get; set; }
    public int? BedTempInitialLayer { get; set; }
    public int? ChamberTemp { get; set; }

    public decimal? MaxVolumetricSpeed { get; set; }
    public decimal? FilamentFlowRatio { get; set; }
    public decimal? PressureAdvance { get; set; }

    public decimal? RetractionLength { get; set; }
    public int? RetractionSpeed { get; set; }
    public decimal? ZHop { get; set; }

    public int? FanMinSpeed { get; set; }
    public int? FanMaxSpeed { get; set; }
    public int? FanDisableFirstLayers { get; set; }
    public int? OverhangFanSpeed { get; set; }

    public string? FilamentStartGcode { get; set; }
    public string? FilamentEndGcode { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

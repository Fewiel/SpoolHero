using System.Globalization;
using System.Text;
using System.Text.Json;
using SpoolManager.Shared.DTOs.Spoolman;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Services;

// Maps SpoolHero spools onto the Spoolman data model. SpoolHero has no separate filament/vendor
// entities with integer ids, so each spool exposes "its" filament under the spool's spoolman id,
// and vendors get a stable id derived from the brand name.
public static class SpoolmanMapper
{
    private const decimal DefaultDiameterMm = 1.75m;

    private static readonly Dictionary<string, decimal> TypicalDensities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PLA"] = 1.24m, ["PETG"] = 1.27m, ["PET"] = 1.27m, ["ABS"] = 1.04m, ["ASA"] = 1.07m,
        ["TPU"] = 1.21m, ["PA"] = 1.14m, ["NYLON"] = 1.14m, ["PC"] = 1.20m, ["PVA"] = 1.23m,
        ["HIPS"] = 1.04m, ["PP"] = 0.90m,
    };

    public static string FormatDate(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // Spoolman requires a density; fall back to a typical value for the material family.
    public static decimal GetDensity(FilamentMaterial? material)
    {
        if (material?.DensityGCm3 is > 0)
            return material.DensityGCm3.Value;
        var type = (material?.Type ?? string.Empty).Trim();
        foreach (var (prefix, density) in TypicalDensities.OrderByDescending(kv => kv.Key.Length))
        {
            if (type.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return density;
        }
        return 1.24m;
    }

    public static decimal GetDiameter(FilamentMaterial? material) =>
        material?.DiameterMm is > 0 ? material.DiameterMm : DefaultDiameterMm;

    // grams = cross-section [mm²] * length [mm] * density [g/cm³] / 1000
    public static decimal LengthToWeight(decimal lengthMm, FilamentMaterial? material)
    {
        var r = GetDiameter(material) / 2m;
        return (decimal)Math.PI * r * r * lengthMm * GetDensity(material) / 1000m;
    }

    public static decimal WeightToLength(decimal grams, FilamentMaterial? material)
    {
        var r = GetDiameter(material) / 2m;
        var gramsPerMm = (decimal)Math.PI * r * r * GetDensity(material) / 1000m;
        return gramsPerMm > 0 ? grams / gramsPerMm : 0;
    }

    public static int VendorId(string vendorName)
    {
        // FNV-1a: string.GetHashCode() is randomized per process, ids must stay stable across restarts.
        uint hash = 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(vendorName.Trim().ToLowerInvariant()))
        {
            hash ^= b;
            hash *= 16777619;
        }
        return (int)(hash & 0x7FFFFFFF);
    }

    public static SpoolmanVendorResponse ToVendor(FilamentMaterial? material)
    {
        var name = string.IsNullOrWhiteSpace(material?.Brand) ? "Unknown" : material.Brand.Trim();
        return new SpoolmanVendorResponse
        {
            Id = VendorId(name),
            Registered = FormatDate(material?.CreatedAt ?? DateTime.UnixEpoch),
            Name = name,
        };
    }

    public static SpoolmanFilamentResponse ToFilament(Spool spool)
    {
        var material = spool.FilamentMaterial;
        var nameParts = new[] { material?.Type, material?.ColorName }.Where(p => !string.IsNullOrWhiteSpace(p));
        return new SpoolmanFilamentResponse
        {
            Id = spool.SpoolmanId,
            Registered = FormatDate(material?.CreatedAt ?? spool.CreatedAt),
            Name = string.Join(" ", nameParts),
            Vendor = ToVendor(material),
            Material = material?.Type ?? string.Empty,
            Price = material?.PricePerKg is { } perKg && material.WeightGrams is > 0
                ? Math.Round(perKg * material.WeightGrams.Value / 1000m, 2)
                : null,
            Density = GetDensity(material),
            Diameter = GetDiameter(material),
            Weight = material?.WeightGrams,
            SettingsExtruderTemp = material?.MaxTempCelsius > 0 ? material.MaxTempCelsius : null,
            SettingsBedTemp = material?.BedTempCelsius,
            ColorHex = (material?.ColorHex ?? "000000").TrimStart('#').ToUpperInvariant(),
            Comment = material?.Notes,
            Extra = new Dictionary<string, string>
            {
                ["spoolhero_material_id"] = JsonSerializer.Serialize(material?.Id.ToString() ?? string.Empty),
            },
        };
    }

    public static SpoolmanSpoolResponse ToSpool(Spool spool)
    {
        var material = spool.FilamentMaterial;
        decimal? initial = material?.WeightGrams is > 0 ? material.WeightGrams.Value : null;
        var remaining = spool.RemainingWeightGrams;
        var used = initial.HasValue ? Math.Max(0, initial.Value - remaining) : 0;

        return new SpoolmanSpoolResponse
        {
            Id = spool.SpoolmanId,
            Registered = FormatDate(spool.CreatedAt),
            FirstUsed = spool.OpenedAt.HasValue ? FormatDate(spool.OpenedAt.Value) : null,
            LastUsed = FormatDate(spool.UpdatedAt),
            Filament = ToFilament(spool),
            Price = spool.PurchasePrice,
            InitialWeight = initial,
            RemainingWeight = remaining,
            UsedWeight = used,
            RemainingLength = Math.Round(WeightToLength(remaining, material), 1),
            UsedLength = Math.Round(WeightToLength(used, material), 1),
            Location = spool.StorageLocation?.Name ?? spool.Printer?.Name ?? spool.Dryer?.Name,
            Comment = spool.Notes,
            Archived = spool.ConsumedAt != null,
            // Spoolman extra values are JSON-encoded strings.
            Extra = new Dictionary<string, string>
            {
                ["spoolhero_id"] = JsonSerializer.Serialize(spool.Id.ToString()),
            },
        };
    }
}

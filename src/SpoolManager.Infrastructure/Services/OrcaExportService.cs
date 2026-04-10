using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Infrastructure.Services;

public interface IOrcaExportService
{
    byte[] ExportSingle(FilamentMaterialDto material);
    byte[] ExportMultipleAsZip(List<FilamentMaterialDto> materials);
    string BuildFileName(FilamentMaterialDto material);
}

public partial class OrcaExportService : IOrcaExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<string, string> InheritsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PLA"] = "Generic PLA",
        ["PETG"] = "Generic PETG",
        ["ABS"] = "Generic ABS",
        ["ASA"] = "Generic ASA",
        ["TPU"] = "Generic TPU",
        ["PA"] = "Generic PA-CF",
        ["Nylon"] = "Generic PA-CF",
        ["PC"] = "Generic PC",
        ["PVA"] = "Generic PVA",
        ["HIPS"] = "Generic HIPS",
        ["PP"] = "Generic PLA",
        ["PET"] = "Generic PETG"
    };

    public byte[] ExportSingle(FilamentMaterialDto material)
    {
        var dict = BuildOrcaDict(material);
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] ExportMultipleAsZip(List<FilamentMaterialDto> materials)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var material in materials)
            {
                var entry = archive.CreateEntry(BuildFileName(material));
                var jsonBytes = ExportSingle(material);
                using var entryStream = entry.Open();
                entryStream.Write(jsonBytes, 0, jsonBytes.Length);
            }
        }
        return ms.ToArray();
    }

    public string BuildFileName(FilamentMaterialDto material)
    {
        var name = BuildPresetName(material);
        var safe = FileNameRegex().Replace(name, "_");
        return $"{safe}.json";
    }

    private static Dictionary<string, object> BuildOrcaDict(FilamentMaterialDto material)
    {
        var presetName = BuildPresetName(material);
        var inherits = ResolveInherits(material.Type);

        var dict = new Dictionary<string, object>
        {
            ["name"] = presetName,
            ["from"] = "User",
            ["inherits"] = inherits,
            ["is_custom_defined"] = "0",
            ["version"] = "2.2.43.2",
            ["filament_settings_id"] = Arr(presetName),
            ["filament_vendor"] = Arr(material.Brand),
            ["filament_type"] = Arr(material.Type),
            ["default_filament_colour"] = Arr($"#{material.ColorHex}"),
            ["nozzle_temperature_range_low"] = Arr(material.MinTempCelsius.ToString(CultureInfo.InvariantCulture)),
            ["nozzle_temperature_range_high"] = Arr(material.MaxTempCelsius.ToString(CultureInfo.InvariantCulture)),
            ["nozzle_temperature"] = Arr(((material.MinTempCelsius + material.MaxTempCelsius) / 2).ToString(CultureInfo.InvariantCulture)),
            ["nozzle_temperature_initial_layer"] = Arr(material.MaxTempCelsius.ToString(CultureInfo.InvariantCulture))
        };

        if (material.BedTempCelsius.HasValue)
        {
            var bed = material.BedTempCelsius.Value.ToString(CultureInfo.InvariantCulture);
            dict["hot_plate_temp"] = Arr(bed);
            dict["hot_plate_temp_initial_layer"] = Arr(bed);
            dict["textured_plate_temp"] = Arr(bed);
            dict["textured_plate_temp_initial_layer"] = Arr(bed);
        }

        if (material.DensityGCm3.HasValue)
            dict["filament_density"] = Arr(material.DensityGCm3.Value.ToString(CultureInfo.InvariantCulture));

        if (material.PricePerKg.HasValue)
            dict["filament_cost"] = Arr(material.PricePerKg.Value.ToString(CultureInfo.InvariantCulture));

        if (material.DiameterMm != 1.75m)
            dict["filament_diameter"] = Arr(material.DiameterMm.ToString(CultureInfo.InvariantCulture));

        return dict;
    }

    private static string BuildPresetName(FilamentMaterialDto material)
    {
        var name = $"{material.Brand} {material.Type}";
        if (!string.IsNullOrWhiteSpace(material.ColorName))
            name += $" - {material.ColorName}";
        return name;
    }

    private static string ResolveInherits(string type)
    {
        if (InheritsMap.TryGetValue(type, out var mapped))
            return mapped;

        foreach (var kvp in InheritsMap)
        {
            if (type.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return "Generic PLA";
    }

    private static string[] Arr(string value) => [value];

    [GeneratedRegex(@"[^a-zA-Z0-9_\-\.]")]
    private static partial Regex FileNameRegex();
}

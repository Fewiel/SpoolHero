using System.Globalization;
using LinqToDB;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services;

public interface IOfdSyncService
{
    Task<OfdSyncResult> SyncAsync();
}

public class OfdSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
}

public class OfdSyncService : IOfdSyncService
{
    private readonly HttpClient _http;
    private readonly SpoolManagerDb _db;
    private const string BaseUrl = "http://api.openfilamentdatabase.org/csv/";

    public OfdSyncService(HttpClient http, SpoolManagerDb db)
    {
        _http = http;
        _db = db;
    }

    public async Task<OfdSyncResult> SyncAsync()
    {
        var brandsCsv = await _http.GetStringAsync($"{BaseUrl}brands.csv");
        var filamentsCsv = await _http.GetStringAsync($"{BaseUrl}filaments.csv");
        var variantsCsv = await _http.GetStringAsync($"{BaseUrl}variants.csv");

        var brands = ParseCsv(brandsCsv);
        var filaments = ParseCsv(filamentsCsv);
        var variants = ParseCsv(variantsCsv);

        var brandMap = brands.ToDictionary(b => b["id"], b => b["name"]);

        var filamentMap = filaments.ToDictionary(f => f["id"]);

        var existingOfdIds = await _db.FilamentMaterials
            .Where(m => m.OfdVariantId != null)
            .Select(m => new { m.OfdVariantId, m.Id })
            .ToListAsync();
        var existingMap = existingOfdIds.ToDictionary(x => x.OfdVariantId!, x => x.Id);

        var result = new OfdSyncResult();

        foreach (var variant in variants)
        {
            var filamentId = variant["filament_id"];
            if (!filamentMap.TryGetValue(filamentId, out var filament))
                continue;

            var brandId = filament["brand_id"];
            if (!brandMap.TryGetValue(brandId, out var brandName))
                continue;

            var variantId = variant["id"];
            var colorHex = (variant.GetValueOrDefault("color_hex") ?? "FFFFFF").TrimStart('#');
            if (string.IsNullOrWhiteSpace(colorHex))
                colorHex = "FFFFFF";

            var materialType = filament.GetValueOrDefault("material") ?? "PLA";
            var minTemp = ParseInt(filament.GetValueOrDefault("min_print_temperature"));
            var maxTemp = ParseInt(filament.GetValueOrDefault("max_print_temperature"));
            var bedTemp = ParseIntNullable(filament.GetValueOrDefault("max_bed_temperature"));
            var density = ParseDecimalNullable(filament.GetValueOrDefault("density"));
            var dryTemp = ParseIntNullable(filament.GetValueOrDefault("max_dry_temperature"));

            if (existingMap.ContainsKey(variantId))
            {
                result.Skipped++;
                continue;
            }

            var material = new FilamentMaterial
            {
                Id = Guid.NewGuid(),
                ProjectId = null,
                Type = materialType,
                Brand = brandName,
                ColorHex = colorHex,
                ColorName = variant.GetValueOrDefault("name"),
                MinTempCelsius = minTemp,
                MaxTempCelsius = maxTemp,
                BedTempCelsius = bedTemp,
                DiameterMm = 1.75m,
                DensityGCm3 = density,
                DryTempCelsius = dryTemp,
                IsPublic = true,
                OfdFilamentId = filamentId,
                OfdVariantId = variantId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _db.InsertAsync(material);
            result.Created++;
        }

        return result;
    }

    private static List<Dictionary<string, string>> ParseCsv(string csv)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return [];

        var headers = ParseCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>();
            for (var j = 0; j < headers.Length && j < values.Length; j++)
                row[headers[j]] = values[j];
            rows.Add(row);
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = "";
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.Trim());
                current = "";
            }
            else if (c == '\r')
            {
                continue;
            }
            else
            {
                current += c;
            }
        }

        fields.Add(current.Trim());
        return fields.ToArray();
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static int? ParseIntNullable(string? value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? ParseDecimalNullable(string? value) =>
        decimal.TryParse(value, CultureInfo.InvariantCulture, out var v) ? v : null;
}

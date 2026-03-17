using System.Text.Json;
using System.Text.Json.Serialization;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Services;

public interface ISpoolmanDbSyncService
{
    Task<int> SyncNowAsync(CancellationToken cancellationToken = default);
}

public class SpoolmanDbSyncService : BackgroundService, ISpoolmanDbSyncService
{
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SpoolmanDbSyncService> _logger;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private const string FilamentsUrl = "https://donkie.github.io/SpoolmanDB/filaments.json";

    public SpoolmanDbSyncService(IServiceProvider services, IHttpClientFactory httpClientFactory,
        ILogger<SpoolmanDbSyncService> logger)
    {
        _services = services;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncNowAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SpoolmanDB sync failed");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    public async Task<int> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting SpoolmanDB sync...");

        var client = _httpClientFactory.CreateClient();
        var json = await client.GetStringAsync(FilamentsUrl, cancellationToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var filaments = JsonSerializer.Deserialize<List<SpoolmanDbFilament>>(json, options);
        if (filaments == null || filaments.Count == 0)
        {
            _logger.LogWarning("SpoolmanDB returned empty filament list");
            return 0;
        }

        var materials = filaments.Select(MapToMaterial).Where(m => m != null).Select(m => m!).ToList();

        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMaterialRepository>();
        await repo.BulkUpsertSyncedAsync(materials);

        _logger.LogInformation("SpoolmanDB sync complete: {Count} materials", materials.Count);
        return materials.Count;
    }

    private static FilamentMaterial? MapToMaterial(SpoolmanDbFilament f)
    {
        if (string.IsNullOrWhiteSpace(f.Id)) return null;

        var colorHex = "CCCCCC";
        if (!string.IsNullOrWhiteSpace(f.ColorHex))
            colorHex = f.ColorHex.TrimStart('#');

        var material = new FilamentMaterial
        {
            Source = "spoolmandb",
            ExternalId = f.Id,
            ProjectId = null,
            Brand = f.Manufacturer ?? "Unknown",
            Type = MapMaterialType(f.Material),
            ColorHex = colorHex,
            ColorName = f.Name,
            ProductName = f.Name,
            DiameterMm = f.Diameter ?? 1.75m,
            WeightGrams = f.Weight,
            SpoolWeightGrams = f.SpoolWeight,
            SpoolType = f.SpoolType,
            MinTempCelsius = f.ExtruderTemp?.Min ?? 0,
            MaxTempCelsius = f.ExtruderTemp?.Max ?? 0,
            BedTempCelsius = f.BedTemp?.Max,
            DensityGCm3 = f.Density,
            Finish = f.Finish,
            Translucent = f.Translucent ?? false,
            Glow = f.Glow ?? false,
            Fill = f.Fill,
            IsPublic = true
        };

        return material;
    }

    private static string MapMaterialType(string? material)
    {
        if (string.IsNullOrWhiteSpace(material)) return "PLA";
        return material.ToUpperInvariant() switch
        {
            "PLA" => "PLA",
            "PETG" => "PETG",
            "ABS" => "ABS",
            "ASA" => "ASA",
            "TPU" => "TPU",
            "NYLON" or "PA" => "Nylon",
            "PC" => "PC",
            "PVA" => "PVA",
            "HIPS" => "HIPS",
            "PP" => "PP",
            "PET" => "PET",
            _ => material
        };
    }
}

internal class SpoolmanDbFilament
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("material")]
    public string? Material { get; set; }

    [JsonPropertyName("density")]
    public decimal? Density { get; set; }

    [JsonPropertyName("weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("spool_weight")]
    public int? SpoolWeight { get; set; }

    [JsonPropertyName("spool_type")]
    public string? SpoolType { get; set; }

    [JsonPropertyName("diameter")]
    public decimal? Diameter { get; set; }

    [JsonPropertyName("color_hex")]
    public string? ColorHex { get; set; }

    [JsonPropertyName("extruder_temp")]
    public TempRange? ExtruderTemp { get; set; }

    [JsonPropertyName("bed_temp")]
    public TempRange? BedTemp { get; set; }

    [JsonPropertyName("finish")]
    public string? Finish { get; set; }

    [JsonPropertyName("translucent")]
    public bool? Translucent { get; set; }

    [JsonPropertyName("glow")]
    public bool? Glow { get; set; }

    [JsonPropertyName("fill")]
    public string? Fill { get; set; }
}

internal class TempRange
{
    [JsonPropertyName("min")]
    public int Min { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }
}

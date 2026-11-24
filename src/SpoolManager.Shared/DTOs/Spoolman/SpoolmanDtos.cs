using System.Text.Json.Serialization;

namespace SpoolManager.Shared.DTOs.Spoolman;

// Wire format of the Spoolman REST API (https://github.com/Donkie/Spoolman), used by Moonraker/Klipper,
// nfc2klipper, SpoolPainter etc. Property names must stay snake_case.

public class SpoolmanSpoolResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("registered")]
    public string Registered { get; set; } = string.Empty;

    [JsonPropertyName("first_used")]
    public string? FirstUsed { get; set; }

    [JsonPropertyName("last_used")]
    public string? LastUsed { get; set; }

    [JsonPropertyName("filament")]
    public SpoolmanFilamentResponse Filament { get; set; } = new();

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("initial_weight")]
    public decimal? InitialWeight { get; set; }

    [JsonPropertyName("spool_weight")]
    public decimal? SpoolWeight { get; set; }

    [JsonPropertyName("remaining_weight")]
    public decimal RemainingWeight { get; set; }

    [JsonPropertyName("used_weight")]
    public decimal UsedWeight { get; set; }

    [JsonPropertyName("remaining_length")]
    public decimal RemainingLength { get; set; }

    [JsonPropertyName("used_length")]
    public decimal UsedLength { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("lot_nr")]
    public string? LotNr { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("extra")]
    public Dictionary<string, string> Extra { get; set; } = new();
}

public class SpoolmanFilamentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("registered")]
    public string Registered { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("vendor")]
    public SpoolmanVendorResponse Vendor { get; set; } = new();

    [JsonPropertyName("material")]
    public string Material { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("density")]
    public decimal Density { get; set; }

    [JsonPropertyName("diameter")]
    public decimal Diameter { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    [JsonPropertyName("spool_weight")]
    public decimal? SpoolWeight { get; set; }

    [JsonPropertyName("settings_extruder_temp")]
    public int? SettingsExtruderTemp { get; set; }

    [JsonPropertyName("settings_bed_temp")]
    public int? SettingsBedTemp { get; set; }

    [JsonPropertyName("color_hex")]
    public string ColorHex { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("extra")]
    public Dictionary<string, string> Extra { get; set; } = new();
}

public class SpoolmanVendorResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("registered")]
    public string Registered { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("extra")]
    public Dictionary<string, string> Extra { get; set; } = new();
}

public class SpoolmanUseRequest
{
    [JsonPropertyName("use_weight")]
    public decimal? UseWeight { get; set; }

    [JsonPropertyName("use_length")]
    public decimal? UseLength { get; set; }
}

public class SpoolmanHealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "healthy";
}

public class SpoolmanInfoResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.22.1";

    [JsonPropertyName("debug_mode")]
    public bool DebugMode { get; set; }

    [JsonPropertyName("automatic_backups")]
    public bool AutomaticBackups { get; set; }

    [JsonPropertyName("data_dir")]
    public string DataDir { get; set; } = string.Empty;

    [JsonPropertyName("logs_dir")]
    public string LogsDir { get; set; } = string.Empty;

    [JsonPropertyName("backups_dir")]
    public string BackupsDir { get; set; } = string.Empty;

    [JsonPropertyName("db_type")]
    public string DbType { get; set; } = "mysql";

    [JsonPropertyName("git_commit")]
    public string? GitCommit { get; set; }

    [JsonPropertyName("build_date")]
    public string? BuildDate { get; set; }
}

public class SpoolmanEventMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "updated";

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = "spool";

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

// SpoolHero-internal DTOs for managing API keys

public class SpoolmanApiKeyDto
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateSpoolmanApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
}

public class SpoolmanCallLogDto
{
    public DateTime CalledAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}

using System.Text.Json.Serialization;

namespace SpoolManager.Shared.DTOs.Spoolman;

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

    [JsonPropertyName("remaining_weight")]
    public decimal RemainingWeight { get; set; }

    [JsonPropertyName("used_weight")]
    public decimal UsedWeight { get; set; }

    [JsonPropertyName("remaining_length")]
    public decimal RemainingLength { get; set; }

    [JsonPropertyName("used_length")]
    public decimal UsedLength { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("extra")]
    public Dictionary<string, string> Extra { get; set; } = new();
}

public class SpoolmanFilamentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("material")]
    public string Material { get; set; } = string.Empty;

    [JsonPropertyName("vendor")]
    public SpoolmanVendorResponse Vendor { get; set; } = new();

    [JsonPropertyName("color_hex")]
    public string ColorHex { get; set; } = string.Empty;

    [JsonPropertyName("diameter")]
    public decimal Diameter { get; set; }

    [JsonPropertyName("density")]
    public decimal Density { get; set; }

    [JsonPropertyName("weight")]
    public decimal Weight { get; set; }

    [JsonPropertyName("spool_weight")]
    public decimal SpoolWeight { get; set; }

    [JsonPropertyName("settings_extruder_temp")]
    public int SettingsExtruderTemp { get; set; }

    [JsonPropertyName("settings_bed_temp")]
    public int SettingsBedTemp { get; set; }
}

public class SpoolmanVendorResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
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
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("automatic_backups")]
    public bool AutomaticBackups { get; set; }

    [JsonPropertyName("data_dir")]
    public string DataDir { get; set; } = string.Empty;

    [JsonPropertyName("logs_dir")]
    public string LogsDir { get; set; } = string.Empty;
}

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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services;

public interface IOpenSpoolService
{
    byte[] Encode(FilamentMaterial material, Guid? spoolId = null);
    byte[] EncodeEntityTag(string entityType, Guid entityId);
    (FilamentMaterial material, bool isValid) Decode(byte[] ndefBytes);
    string ToJson(FilamentMaterial material, Guid? spoolId = null);
    (FilamentMaterial? material, bool isValid, string rawJson, Guid? spoolId) FromJson(string json);
}

public class OpenSpoolService : IOpenSpoolService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson(FilamentMaterial material, Guid? spoolId = null)
    {
        var payload = new OpenSpoolPayload
        {
            Protocol = "openspool",
            Version = "1.0",
            Type = material.Type,
            ColorHex = NearestOpenSpoolHex(material.ColorHex),
            Brand = material.Brand,
            MinTemp = material.MinTempCelsius,
            MaxTemp = material.MaxTempCelsius,
            SmSpoolId = spoolId?.ToString()
        };
        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private static string NearestOpenSpoolHex(string colorHex)
    {
        var clean = colorHex.TrimStart('#');
        if (clean.Length != 6) return colorHex;

        if (_openSpoolColors.Contains(clean.ToUpperInvariant()))
            return clean.ToUpperInvariant();

        if (!TryParseHex(clean, out var r, out var g, out var b))
            return colorHex;

        var best = _openSpoolColors[0];
        var bestDist = double.MaxValue;
        foreach (var hex in _openSpoolColors)
        {
            if (!TryParseHex(hex, out var cr, out var cg, out var cb)) continue;
            var dist = Math.Pow(r - cr, 2) + Math.Pow(g - cg, 2) + Math.Pow(b - cb, 2);
            if (dist < bestDist) { bestDist = dist; best = hex; }
        }
        return best;
    }

    private static bool TryParseHex(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (hex.Length != 6) return false;
        try
        {
            r = Convert.ToInt32(hex[..2], 16);
            g = Convert.ToInt32(hex[2..4], 16);
            b = Convert.ToInt32(hex[4..6], 16);
            return true;
        }
        catch { return false; }
    }

    private static readonly string[] _openSpoolColors =
    [
        "FFFFFF", "FFF144", "DCF478", "0ACC38", "057748", "0D6284",
        "0EE2A0", "76D9F4", "46A8F9", "2850E0", "443089", "A03CF7",
        "F330F9", "D4B1DD", "F95D73", "F72323", "7C4B00", "F98C36",
        "FCECD6", "D3C5A3", "AF7933", "898989", "BCBCBC", "161616"
    ];

    public (FilamentMaterial? material, bool isValid, string rawJson, Guid? spoolId) FromJson(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<OpenSpoolPayload>(json, _jsonOptions);
            if (payload?.Protocol != "openspool") return (null, false, json, null);

            var material = new FilamentMaterial
            {
                Type = payload.Type ?? string.Empty,
                ColorHex = payload.ColorHex ?? "FFFFFF",
                Brand = payload.Brand ?? string.Empty,
                MinTempCelsius = payload.MinTemp ?? 0,
                MaxTempCelsius = payload.MaxTemp ?? 0
            };
            Guid? spoolId = Guid.TryParse(payload.SmSpoolId, out var parsed) ? parsed : null;
            return (material, true, json, spoolId);
        }
        catch
        {
            return (null, false, json, null);
        }
    }

    public byte[] Encode(FilamentMaterial material, Guid? spoolId = null)
    {
        var json = ToJson(material, spoolId);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var typeBytes = Encoding.UTF8.GetBytes("application/json");
        return BuildNdefMessage(typeBytes, jsonBytes);
    }

    public byte[] EncodeEntityTag(string entityType, Guid entityId)
    {
        var payload = new { protocol = "spoolmanager", type = entityType, id = entityId.ToString() };
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var typeBytes = Encoding.UTF8.GetBytes("application/json");
        return BuildNdefMessage(typeBytes, jsonBytes);
    }

    public (FilamentMaterial material, bool isValid) Decode(byte[] ndefBytes)
    {
        try
        {
            var json = ExtractJsonFromNdef(ndefBytes);
            if (json == null) return (new FilamentMaterial(), false);

            var (material, isValid, _, _) = FromJson(json);
            return (material ?? new FilamentMaterial(), isValid);
        }
        catch
        {
            return (new FilamentMaterial(), false);
        }
    }

    private static byte[] BuildNdefMessage(byte[] typeBytes, byte[] payload)
    {
        var typeLength = (byte)typeBytes.Length;
        bool isShortRecord = payload.Length <= 255;

        var flags = (byte)(0xD0 | (isShortRecord ? 0x10 : 0x00) | 0x02);

        using var ms = new MemoryStream();
        ms.WriteByte(flags);
        ms.WriteByte(typeLength);

        if (isShortRecord)
        {
            ms.WriteByte((byte)payload.Length);
        }
        else
        {
            var lenBytes = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            ms.Write(lenBytes, 0, 4);
        }

        ms.Write(typeBytes, 0, typeBytes.Length);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    private static string? ExtractJsonFromNdef(byte[] data)
    {
        if (data.Length < 3) return null;

        var pos = 0;
        var flags = data[pos++];
        var typeLength = data[pos++];
        bool isShortRecord = (flags & 0x10) != 0;

        int payloadLength;
        if (isShortRecord)
        {
            payloadLength = data[pos++];
        }
        else
        {
            if (pos + 4 > data.Length) return null;
            payloadLength = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
        }

        pos += typeLength;
        if (pos + payloadLength > data.Length) return null;

        return Encoding.UTF8.GetString(data, pos, payloadLength);
    }

    private class OpenSpoolPayload
    {
        public string? Protocol { get; set; }
        public string? Version { get; set; }
        public string? Type { get; set; }
        public string? ColorHex { get; set; }
        public string? Brand { get; set; }
        public int? MinTemp { get; set; }
        public int? MaxTemp { get; set; }
        [JsonPropertyName("_sm_spool_id")]
        public string? SmSpoolId { get; set; }
    }
}

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SpoolManager.Shared.DTOs.Materials;

namespace SpoolManager.Infrastructure.Services;

public interface IMaterialExportService
{
    string ExportToBase64(IEnumerable<FilamentMaterialDto> materials);
    (List<FilamentMaterialDto> materials, string? error) ImportFromBase64(string base64);
}

public class MaterialExportService : IMaterialExportService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public string ExportToBase64(IEnumerable<FilamentMaterialDto> materials)
    {
        var payload = new MaterialExportPayload
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            Materials = materials.ToList()
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.SmallestSize))
            gz.Write(jsonBytes, 0, jsonBytes.Length);

        return Convert.ToBase64String(output.ToArray());
    }

    public (List<FilamentMaterialDto> materials, string? error) ImportFromBase64(string base64)
    {
        try
        {
            var compressed = Convert.FromBase64String(base64.Trim());

            using var input = new MemoryStream(compressed);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new MemoryStream();
            gz.CopyTo(reader);

            var json = Encoding.UTF8.GetString(reader.ToArray());
            var payload = JsonSerializer.Deserialize<MaterialExportPayload>(json, _jsonOptions);

            if (payload?.Materials == null)
                return ([], "Invalid export format.");

            foreach (var m in payload.Materials)
            {
                m.Id = Guid.Empty;
                m.CreatedAt = default;
                m.UpdatedAt = default;
            }

            return (payload.Materials, null);
        }
        catch (Exception ex)
        {
            return ([], $"Import failed: {ex.Message}");
        }
    }
}

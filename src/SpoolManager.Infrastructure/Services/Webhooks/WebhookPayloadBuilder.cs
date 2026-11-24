using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services.Webhooks;

/// <summary>
/// Builds the provider specific JSON bodies. Messages are English on purpose
/// (receivers are machines or shared channels); structured data is always included.
/// </summary>
public static class WebhookPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Payloads go to APIs, not HTML pages: keep quotes/umlauts readable instead of \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Title(WebhookEventData e) => e.EventType switch
    {
        WebhookEvents.DryerFinished => "Dryer finished",
        WebhookEvents.SpoolLow => "Spool running low",
        WebhookEvents.SpoolConsumed => "Spool consumed",
        WebhookEvents.Test => "Test notification",
        _ => e.EventType
    };

    public static string Message(WebhookEventData e, string? projectName)
    {
        var prefix = string.IsNullOrWhiteSpace(projectName) ? "" : $"[{projectName}] ";
        return prefix + e.EventType switch
        {
            WebhookEvents.DryerFinished => $"Dryer \"{e.Dryer?.Name}\" has finished drying.",
            WebhookEvents.SpoolLow => $"Spool {MaterialLabel(e)} is running low: {Num(e.Spool?.RemainingPercent)}% remaining ({Num(e.Spool?.RemainingWeightGrams)} g).",
            WebhookEvents.SpoolConsumed => $"Spool {MaterialLabel(e)} has been marked as consumed.",
            WebhookEvents.Test => "This is a test notification from SpoolHero. Your webhook works!",
            _ => e.EventType
        };
    }

    private static string MaterialLabel(WebhookEventData e)
    {
        if (e.Material == null)
            return e.Spool != null ? $"#{e.Spool.Id}" : "";
        var parts = new[] { e.Material.Brand, e.Material.Type, e.Material.ColorName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(' ', parts);
    }

    private static string Num(decimal? d) =>
        d.HasValue ? d.Value.ToString("0.#", CultureInfo.InvariantCulture) : "?";

    /// <summary>Structured payload used by Generic and Home Assistant webhooks.</summary>
    public static string BuildStructured(WebhookEventData e, string? projectName)
    {
        var payload = new Dictionary<string, object?>
        {
            ["event"] = e.EventType,
            ["title"] = Title(e),
            ["message"] = Message(e, projectName),
            ["timestamp"] = Utc(e.OccurredAt),
            ["source"] = "SpoolHero",
            ["project"] = new Dictionary<string, object?> { ["id"] = e.ProjectId, ["name"] = projectName }
        };
        if (e.Spool != null)
            payload["spool"] = new Dictionary<string, object?>
            {
                ["id"] = e.Spool.Id,
                ["remaining_percent"] = e.Spool.RemainingPercent,
                ["remaining_weight_grams"] = e.Spool.RemainingWeightGrams,
                ["rfid_tag_uid"] = e.Spool.RfidTagUid,
                ["owner"] = e.Spool.OwnerName,
                ["consumed_at"] = Utc(e.Spool.ConsumedAt)
            };
        if (e.Material != null)
            payload["material"] = new Dictionary<string, object?>
            {
                ["id"] = e.Material.Id,
                ["brand"] = e.Material.Brand,
                ["type"] = e.Material.Type,
                ["color_name"] = e.Material.ColorName,
                ["color_hex"] = "#" + e.Material.ColorHex.TrimStart('#'),
                ["diameter_mm"] = e.Material.DiameterMm
            };
        if (e.Dryer != null)
            payload["dryer"] = new Dictionary<string, object?>
            {
                ["id"] = e.Dryer.Id,
                ["name"] = e.Dryer.Name,
                ["drying_started_at"] = Utc(e.Dryer.DryingStartedAt),
                ["drying_finish_at"] = Utc(e.Dryer.DryingFinishAt)
            };
        return JsonSerializer.Serialize(RemoveNulls(payload), JsonOptions);
    }

    public static string BuildDiscord(WebhookEventData e, string? projectName)
    {
        var fields = new List<object>();
        if (!string.IsNullOrWhiteSpace(projectName))
            fields.Add(new { name = "Project", value = projectName, inline = true });
        if (e.Material != null)
        {
            fields.Add(new { name = "Material", value = Truncate(MaterialLabel(e), 1024), inline = true });
            fields.Add(new { name = "Color", value = "#" + e.Material.ColorHex.TrimStart('#'), inline = true });
        }
        if (e.Spool != null && e.EventType == WebhookEvents.SpoolLow)
            fields.Add(new { name = "Remaining", value = $"{Num(e.Spool.RemainingPercent)}% ({Num(e.Spool.RemainingWeightGrams)} g)", inline = true });
        if (!string.IsNullOrWhiteSpace(e.Spool?.OwnerName))
            fields.Add(new { name = "Owner", value = Truncate(e.Spool.OwnerName, 1024), inline = true });
        if (e.Dryer != null)
            fields.Add(new { name = "Dryer", value = Truncate(e.Dryer.Name, 1024), inline = true });

        var payload = new
        {
            username = "SpoolHero",
            content = Truncate(Message(e, projectName), 2000),
            embeds = new[]
            {
                new
                {
                    title = Title(e),
                    color = EmbedColor(e),
                    timestamp = Utc(e.OccurredAt),
                    fields,
                    footer = new { text = "SpoolHero · " + e.EventType }
                }
            }
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string BuildTelegram(WebhookEventData e, string? projectName, string chatId)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Title(e));
        sb.AppendLine(Message(e, projectName));
        if (e.Material?.ColorHex is { Length: > 0 } hex)
            sb.AppendLine($"Color: #{hex.TrimStart('#')}");
        var payload = new
        {
            chat_id = chatId,
            text = Truncate(sb.ToString().TrimEnd(), 4096),
            disable_web_page_preview = true
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static int EmbedColor(WebhookEventData e)
    {
        if (e.Material != null && int.TryParse(e.Material.ColorHex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var c))
            return c & 0xFFFFFF;
        return e.EventType switch
        {
            WebhookEvents.DryerFinished => 0x198754,
            WebhookEvents.SpoolLow => 0xFFC107,
            WebhookEvents.SpoolConsumed => 0x6C757D,
            _ => 0x0D6EFD
        };
    }

    private static Dictionary<string, object?> RemoveNulls(Dictionary<string, object?> d)
    {
        foreach (var key in d.Keys.ToList())
        {
            if (d[key] == null)
                d.Remove(key);
            else if (d[key] is Dictionary<string, object?> nested)
                RemoveNulls(nested);
        }
        return d;
    }

    // DB values come back with Kind=Unspecified but are stored as UTC.
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? d) => d.HasValue ? Utc(d.Value) : null;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

using System.Net.Http.Json;
using System.Text.Json;

namespace SpoolManager.Client.Services;

/// <summary>Turns API error responses into readable text instead of showing raw JSON.</summary>
public static class ResponseMessages
{
    /// <summary>
    /// Returns the <c>message</c> of a JSON error body (<c>{"message": "..."}</c>), a short plain-text body,
    /// or <paramref name="fallback"/>.
    /// </summary>
    public static async Task<string> ReadAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text))
                return fallback;
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    return msg.GetString() ?? fallback;
                return fallback;
            }
            if (trimmed.StartsWith('"'))
                return JsonSerializer.Deserialize<string>(text) ?? fallback;
            return trimmed.StartsWith('<') || text.Length > 300 ? fallback : text;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}

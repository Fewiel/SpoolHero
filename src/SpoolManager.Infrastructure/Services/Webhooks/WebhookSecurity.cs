using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SpoolManager.Infrastructure.Services.Webhooks;

public class WebhookOptions
{
    /// <summary>Base URL of the Telegram Bot API. Only override for proxies/testing.</summary>
    public string TelegramApiBaseUrl { get; set; } = "https://api.telegram.org";

    /// <summary>Per-request timeout for outgoing webhook calls.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// When true, webhooks may not target loopback or private (RFC1918 / ULA / CGNAT) addresses.
    /// Off by default because self-hosted setups usually talk to a Home Assistant on the LAN.
    /// Link-local (incl. cloud metadata 169.254.169.254), unspecified and multicast are always blocked.
    /// </summary>
    public bool BlockPrivateNetworks { get; set; }
}

public static partial class WebhookSecurity
{
    [GeneratedRegex(@"^\d{3,15}:[A-Za-z0-9_-]{20,100}$")]
    private static partial Regex BotTokenRegex();

    [GeneratedRegex(@"^(-?\d{1,20}|@[A-Za-z][A-Za-z0-9_]{3,64})$")]
    private static partial Regex ChatIdRegex();

    public static bool IsValidUrl(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL is required.";
            return false;
        }
        if (url.Length > 2000)
        {
            error = "URL is too long.";
            return false;
        }
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            error = "Only absolute http:// or https:// URLs are allowed.";
            return false;
        }
        return true;
    }

    public static bool IsValidBotToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && BotTokenRegex().IsMatch(token.Trim());

    public static bool IsValidChatId(string? chatId) =>
        !string.IsNullOrWhiteSpace(chatId) && ChatIdRegex().IsMatch(chatId.Trim());

    /// <summary>"123456:AB…wxyz" – enough to recognise the token, useless to an attacker.</summary>
    public static string? MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        var colon = token.IndexOf(':');
        var botId = colon > 0 ? token[..colon] : "";
        var tail = token.Length >= 4 ? token[^4..] : "";
        return $"{botId}:••••{tail}";
    }

    public static bool IsAllowedAddress(IPAddress address, bool blockPrivate)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            if (b[0] == 0) return false;                               // 0.0.0.0/8
            if (b[0] == 169 && b[1] == 254) return false;              // link-local / cloud metadata
            if (b[0] >= 224) return false;                             // multicast + reserved
            if (!blockPrivate) return true;
            if (b[0] == 127) return false;                             // loopback
            if (b[0] == 10) return false;                              // 10/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false; // 172.16/12
            if (b[0] == 192 && b[1] == 168) return false;              // 192.168/16
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false; // CGNAT 100.64/10
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
                return false;
            if (!blockPrivate) return true;
            if (IPAddress.IsLoopback(address)) return false;
            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;                   // ULA fc00::/7
            return true;
        }

        return false;
    }

    /// <summary>
    /// Connect callback for SocketsHttpHandler: resolves the host and refuses to connect to
    /// disallowed addresses. Checking at connect time also covers DNS rebinding.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CreateConnectCallback(WebhookOptions options) =>
        async (context, ct) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, ct);

            var allowed = addresses.Where(a => IsAllowedAddress(a, options.BlockPrivateNetworks)).ToList();
            if (allowed.Count == 0)
                throw new HttpRequestException($"Webhook target '{host}' resolves to a blocked network address.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed.ToArray(), context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
}

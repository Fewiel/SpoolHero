using System.Net.WebSockets;
using System.Text.RegularExpressions;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Server.Services;

namespace SpoolManager.Server.Middleware;

// Spoolman change-event websockets: /{apiKey}/api/v1/ (all), /{apiKey}/api/v1/spool (all spools)
// and /{apiKey}/api/v1/spool/{id}. Moonraker keeps /api/v1/spool open to get notified about changes.
public partial class SpoolmanWebSocketMiddleware
{
    private readonly RequestDelegate _next;

    public SpoolmanWebSocketMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISpoolmanApiKeyRepository apiKeys,
        ISpoolmanCallLogRepository callLogs, SpoolmanEventHub hub)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var match = WebSocketPath().Match(context.Request.Path.Value ?? string.Empty);
        if (!match.Success)
        {
            await _next(context);
            return;
        }

        var key = await apiKeys.GetByApiKeyAsync(match.Groups["key"].Value);
        if (key == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        int? spoolmanId = match.Groups["id"].Success ? int.Parse(match.Groups["id"].Value) : null;
        var resource = match.Groups["resource"].Success ? match.Groups["resource"].Value : string.Empty;

        try
        {
            await apiKeys.UpdateLastUsedAsync(key.Id);
            await callLogs.LogAsync(key.Id, "WS", $"/api/v1{resource}", StatusCodes.Status101SwitchingProtocols);
        }
        catch
        {
            // Logging must never prevent the connection.
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var subscription = hub.Register(ws, key.ProjectId, spoolmanId);
        try
        {
            await ReceiveUntilClosedAsync(ws, context.RequestAborted);
        }
        finally
        {
            hub.Unregister(subscription);
        }
    }

    private static async Task ReceiveUntilClosedAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    [GeneratedRegex(@"^/(?<key>[^/]+)/api/v1(?<resource>/spool(?:/(?<id>\d{1,9}))?)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex WebSocketPath();
}

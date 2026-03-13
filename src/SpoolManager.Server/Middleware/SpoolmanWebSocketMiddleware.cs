using System.Net.WebSockets;
using System.Text.RegularExpressions;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Middleware;

public partial class SpoolmanWebSocketMiddleware
{
    private readonly RequestDelegate _next;

    public SpoolmanWebSocketMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISpoolmanApiKeyRepository apiKeys)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        var match = SpoolPath().Match(path);
        if (!match.Success)
        {
            await _next(context);
            return;
        }

        var apiKeyValue = match.Groups[1].Value;
        var key = await apiKeys.GetByApiKeyAsync(apiKeyValue);
        if (key == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        await KeepAliveAsync(ws, context.RequestAborted);
    }

    private static async Task KeepAliveAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout — send an empty ping to keep the connection alive
                try
                {
                    await ws.SendAsync(ArraySegment<byte>.Empty, WebSocketMessageType.Text, true, ct);
                }
                catch { break; }
            }
            catch { break; }
        }
    }

    [GeneratedRegex(@"^/([^/]+)/api/v1/spool$", RegexOptions.IgnoreCase)]
    private static partial Regex SpoolPath();
}

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SpoolManager.Shared.DTOs.Spoolman;

namespace SpoolManager.Server.Services;

// Keeps the Spoolman websocket connections (Moonraker subscribes to /api/v1/spool) per project and
// pushes Spoolman-style change events to them.
public class SpoolmanEventHub
{
    private sealed class Subscriber
    {
        public required WebSocket Socket { get; init; }
        public required Guid ProjectId { get; init; }
        public int? SpoolmanId { get; init; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private readonly ILogger<SpoolmanEventHub> _logger;

    public SpoolmanEventHub(ILogger<SpoolmanEventHub> logger) => _logger = logger;

    public Guid Register(WebSocket socket, Guid projectId, int? spoolmanId)
    {
        var id = Guid.NewGuid();
        _subscribers[id] = new Subscriber { Socket = socket, ProjectId = projectId, SpoolmanId = spoolmanId };
        return id;
    }

    public void Unregister(Guid subscriptionId) => _subscribers.TryRemove(subscriptionId, out _);

    public async Task PublishSpoolUpdatedAsync(Guid projectId, SpoolmanSpoolResponse spool)
    {
        var message = new SpoolmanEventMessage
        {
            Type = "updated",
            Resource = "spool",
            Date = SpoolmanMapper.FormatDate(DateTime.UtcNow),
            Payload = spool,
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        foreach (var (id, sub) in _subscribers)
        {
            if (sub.ProjectId != projectId || (sub.SpoolmanId.HasValue && sub.SpoolmanId != spool.Id))
                continue;
            if (sub.Socket.State != WebSocketState.Open)
            {
                Unregister(id);
                continue;
            }

            await sub.SendLock.WaitAsync();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await sub.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Dropping Spoolman websocket subscriber {Id}", id);
                Unregister(id);
            }
            finally
            {
                sub.SendLock.Release();
            }
        }
    }
}

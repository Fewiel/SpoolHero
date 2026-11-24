using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Infrastructure.Services.Webhooks;

public interface IWebhookDispatcher
{
    /// <summary>Queues an event for delivery. Never blocks and never throws.</summary>
    void Enqueue(WebhookEventData evt);
}

/// <summary>
/// In-memory queue + background worker that delivers events to all matching webhooks of a project.
/// Request paths only enqueue; HTTP calls happen here, off the request thread.
/// </summary>
public class WebhookDispatcher : BackgroundService, IWebhookDispatcher
{
    private readonly Channel<WebhookEventData> _queue = Channel.CreateBounded<WebhookEventData>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(IServiceScopeFactory scopeFactory, ILogger<WebhookDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(WebhookEventData evt)
    {
        if (!_queue.Writer.TryWrite(evt))
            _logger.LogWarning("Webhook queue full, dropping {Event} for project {ProjectId}", evt.EventType, evt.ProjectId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DispatchAsync(evt, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dispatching webhook event {Event} for project {ProjectId}", evt.EventType, evt.ProjectId);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task DispatchAsync(WebhookEventData evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var webhooks = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();

        var hooks = await webhooks.GetEnabledForEventAsync(evt.ProjectId, evt.EventType);
        if (hooks.Count == 0)
            return;

        var projectName = (await projects.GetByIdAsync(evt.ProjectId))?.Name;

        // Send in parallel (each call has its own timeout), persist results sequentially
        // because the DB connection is not thread-safe.
        var results = await Task.WhenAll(hooks.Select(async h => (Hook: h, Result: await sender.SendAsync(h, evt, projectName, ct))));
        var now = DateTime.UtcNow;
        foreach (var (hook, result) in results)
        {
            try
            {
                await webhooks.UpdateDeliveryStatusAsync(hook.Id, now, result.Success, result.StatusCode, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not store delivery status for webhook {WebhookId}", hook.Id);
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Security.Claims;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Middleware;

public class UserActivityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly ConcurrentDictionary<Guid, DateTime> _lastPersisted = new();

    public UserActivityMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.User.Identity?.IsAuthenticated != true) return;
        var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return;

        var now = DateTime.UtcNow;
        if (_lastPersisted.TryGetValue(userId, out var last) && (now - last).TotalMinutes < 5) return;

        _lastPersisted[userId] = now;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            await repo.UpdateLastActiveAsync(userId, now);
        }
        catch { }
    }
}

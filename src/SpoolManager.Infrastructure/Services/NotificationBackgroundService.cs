using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Infrastructure.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private DateTime _lastCleanup = DateTime.MinValue;

    public NotificationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var dryers = scope.ServiceProvider.GetRequiredService<IDryerRepository>();
                var spools = scope.ServiceProvider.GetRequiredService<ISpoolRepository>();
                var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                await CheckDryersAsync(dryers, users, email);
                await CheckSpoolsAsync(spools, users, email);

                if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromHours(24))
                {
                    var auditLogs = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                    var cutoff = DateTime.UtcNow.AddDays(-30);
                    var deleted = await auditLogs.DeleteOlderThanAsync(cutoff);
                    if (deleted > 0)
                        _logger.LogInformation("Cleaned up {Count} audit log entries older than 30 days.", deleted);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification background service");
            }
        }
    }

    private static async Task CheckDryersAsync(IDryerRepository dryers, IUserRepository users, IEmailService email)
    {
        var done = await dryers.GetDryersNeedingNotificationAsync();
        foreach (var dryer in done)
        {
            var members = await users.GetProjectMembersAsync(dryer.ProjectId);
            await email.NotifyDryerDoneAsync(dryer, members);
            dryer.DryingNotifiedAt = DateTime.UtcNow;
            await dryers.UpdateAsync(dryer);
        }
    }

    private static async Task CheckSpoolsAsync(ISpoolRepository spools, IUserRepository users, IEmailService email)
    {
        var low = await spools.GetSpoolsNeedingLowNotificationAsync();
        foreach (var spool in low)
        {
            var members = await users.GetProjectMembersAsync(spool.ProjectId);
            await email.NotifySpoolLowAsync(spool, members);
            spool.LowSpoolNotifiedAt = DateTime.UtcNow;
            await spools.UpdateAsync(spool);
        }
    }
}

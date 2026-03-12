using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Shared.DTOs.Admin;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogs;

    public AdminAuditController(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    private bool IsPlatformAdmin() =>
        User.FindFirst("is_platform_admin")?.Value == "true";

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string? action = null,
        [FromQuery] string? user = null)
    {
        if (!IsPlatformAdmin()) return Forbid();

        limit = Math.Clamp(limit, 1, 200);

        var logs = await _auditLogs.GetRecentAsync(limit, offset, action, user);
        var total = await _auditLogs.CountAsync(action, user);

        var dtos = logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            Timestamp = l.Timestamp,
            UserId = l.UserId,
            Username = l.Username,
            Action = l.Action,
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            EntityName = l.EntityName,
            ProjectId = l.ProjectId,
            ProjectName = l.ProjectName,
            Details = l.Details,
            IpAddress = l.IpAddress
        }).ToList();

        return Ok(new AuditLogPageResponse { Logs = dtos, Total = total });
    }
}

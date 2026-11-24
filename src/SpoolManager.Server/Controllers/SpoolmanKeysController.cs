using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Spoolman;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

// Management of the per-project API keys for the Spoolman-compatible API. Keys grant read access to
// the project's spools and let clients book filament usage, so only project admins/managers manage them.
[ApiController]
[Route("api/projects/{projectId:guid}/spoolman/keys")]
[Authorize]
public class SpoolmanKeysController : ControllerBase
{
    private readonly ISpoolmanApiKeyRepository _apiKeys;
    private readonly ISpoolmanCallLogRepository _callLogs;
    private readonly IProjectMemberRepository _members;
    private readonly IAuditService _audit;

    public SpoolmanKeysController(ISpoolmanApiKeyRepository apiKeys, ISpoolmanCallLogRepository callLogs,
        IProjectMemberRepository members, IAuditService audit)
    {
        _apiKeys = apiKeys;
        _callLogs = callLogs;
        _members = members;
        _audit = audit;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private async Task<IActionResult?> CheckAccessAsync(Guid projectId)
    {
        var member = await _members.GetAsync(projectId, UserId);
        if (member == null)
            return StatusCode(403, new { message = "Not a member of this project." });
        if (member.Role != "admin" && member.Role != "manager")
            return StatusCode(403, new { message = "Only admins and managers can manage API keys." });
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId)
    {
        if (await CheckAccessAsync(projectId) is { } denied)
            return denied;

        var keys = await _apiKeys.GetAllByProjectAsync(projectId);
        return Ok(keys.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateSpoolmanApiKeyRequest request)
    {
        if (await CheckAccessAsync(projectId) is { } denied)
            return denied;
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var name = request.Name.Trim();
        var key = new SpoolmanApiKey
        {
            ProjectId = projectId,
            ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            Name = name.Length > 100 ? name[..100] : name,
        };
        await _apiKeys.CreateAsync(key);

        await _audit.LogAsync("spoolman_key.create",
            userId: UserId, username: UserName,
            entityType: "spoolman_api_key", entityId: key.Id.ToString(), entityName: key.Name,
            projectId: projectId, ipAddress: ClientIp);

        return Ok(ToDto(key));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id)
    {
        if (await CheckAccessAsync(projectId) is { } denied)
            return denied;

        var key = await _apiKeys.GetByIdAsync(id, projectId);
        if (key == null)
            return NotFound();

        await _apiKeys.DeleteAsync(id, projectId);
        await _audit.LogAsync("spoolman_key.delete",
            userId: UserId, username: UserName,
            entityType: "spoolman_api_key", entityId: id.ToString(), entityName: key.Name,
            projectId: projectId, ipAddress: ClientIp);
        return NoContent();
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid projectId, Guid id)
    {
        if (await CheckAccessAsync(projectId) is { } denied)
            return denied;
        if (await _apiKeys.GetByIdAsync(id, projectId) == null)
            return NotFound();

        var logs = await _callLogs.GetLast24hAsync(id);
        return Ok(logs.Select(l => new SpoolmanCallLogDto
        {
            CalledAt = l.CalledAt,
            Method = l.Method,
            Path = l.Path,
            StatusCode = l.StatusCode,
        }).ToList());
    }

    private static SpoolmanApiKeyDto ToDto(SpoolmanApiKey k) => new()
    {
        Id = k.Id,
        ApiKey = k.ApiKey,
        Name = k.Name,
        CreatedAt = k.CreatedAt,
        LastUsedAt = k.LastUsedAt,
    };
}

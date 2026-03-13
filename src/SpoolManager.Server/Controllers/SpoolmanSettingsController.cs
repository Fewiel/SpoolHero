using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Spoolman;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/spoolman/apikeys")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class SpoolmanSettingsController : ControllerBase
{
    private readonly ISpoolmanApiKeyRepository _apiKeys;

    public SpoolmanSettingsController(ISpoolmanApiKeyRepository apiKeys)
    {
        _apiKeys = apiKeys;
    }

    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var keys = await _apiKeys.GetAllByProjectAsync(ProjectMember.ProjectId);
        return Ok(keys.Select(k => new SpoolmanApiKeyDto
        {
            Id = k.Id,
            ApiKey = k.ApiKey,
            Name = k.Name,
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt,
        }).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSpoolmanApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var key = new SpoolmanApiKey
        {
            ProjectId = ProjectMember.ProjectId,
            ApiKey = GenerateApiKey(),
            Name = request.Name.Trim(),
        };

        await _apiKeys.CreateAsync(key);

        return Ok(new SpoolmanApiKeyDto
        {
            Id = key.Id,
            ApiKey = key.ApiKey,
            Name = key.Name,
            CreatedAt = key.CreatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _apiKeys.DeleteAsync(id);
        return NoContent();
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

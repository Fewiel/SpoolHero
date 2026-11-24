using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Server.Filters;
using SpoolManager.Server.Services;
using SpoolManager.Shared.DTOs.Spoolman;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

// Spoolman-compatible REST API (subset) so Moonraker/Klipper, nfc2klipper, SpoolPainter etc. can use
// SpoolHero as their Spoolman server. Configure clients with server: https://<host>/<apiKey>
[ApiController]
[Route("{apiKey}/api/v1")]
[ServiceFilter(typeof(SpoolmanAuthFilter))]
public class SpoolmanController : ControllerBase
{
    private readonly ISpoolRepository _spools;
    private readonly SpoolmanEventHub _events;

    public SpoolmanController(ISpoolRepository spools, SpoolmanEventHub events)
    {
        _spools = spools;
        _events = events;
    }

    private Guid ProjectId => (Guid)HttpContext.Items[SpoolmanAuthFilter.ProjectIdItem]!;

    [HttpGet("health")]
    [SpoolmanPublic]
    public IActionResult Health() => Ok(new SpoolmanHealthResponse());

    [HttpGet("info")]
    public IActionResult Info() => Ok(new SpoolmanInfoResponse());

    [HttpGet("spool")]
    public async Task<IActionResult> ListSpools([FromQuery(Name = "allow_archived")] bool allowArchived = false)
    {
        var spools = await _spools.GetAllAsync(ProjectId, consumed: allowArchived ? null : false);
        var result = spools.Select(SpoolmanMapper.ToSpool).ToList();
        Response.Headers["X-Total-Count"] = result.Count.ToString();
        return Ok(result);
    }

    [HttpGet("spool/{id:int}")]
    public async Task<IActionResult> GetSpool(int id)
    {
        var spool = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        return spool == null ? SpoolmanNotFound("Spool", id) : Ok(SpoolmanMapper.ToSpool(spool));
    }

    [HttpPut("spool/{id:int}/use")]
    public async Task<IActionResult> UseSpool(int id, [FromBody] SpoolmanUseRequest request)
    {
        var spool = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        if (spool == null)
            return SpoolmanNotFound("Spool", id);

        if (request.UseWeight.HasValue && request.UseLength.HasValue)
            return BadRequest(new { message = "Only specify either use_weight or use_length." });

        decimal usedGrams;
        if (request.UseWeight.HasValue)
            usedGrams = request.UseWeight.Value;
        else if (request.UseLength.HasValue)
            usedGrams = SpoolmanMapper.LengthToWeight(request.UseLength.Value, spool.FilamentMaterial);
        else
            return BadRequest(new { message = "Either use_weight or use_length must be specified." });

        await _spools.ApplyUsageAsync(spool.Id, usedGrams, spool.FilamentMaterial?.WeightGrams ?? 0);

        var updated = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        var response = SpoolmanMapper.ToSpool(updated!);
        await _events.PublishSpoolUpdatedAsync(ProjectId, response);
        return Ok(response);
    }

    [HttpGet("filament")]
    public async Task<IActionResult> ListFilaments()
    {
        var spools = await _spools.GetAllAsync(ProjectId);
        return Ok(spools.Select(SpoolmanMapper.ToFilament).ToList());
    }

    [HttpGet("filament/{id:int}")]
    public async Task<IActionResult> GetFilament(int id)
    {
        var spool = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        return spool == null ? SpoolmanNotFound("Filament", id) : Ok(SpoolmanMapper.ToFilament(spool));
    }

    [HttpGet("vendor")]
    public async Task<IActionResult> ListVendors()
    {
        var spools = await _spools.GetAllAsync(ProjectId);
        return Ok(Vendors(spools));
    }

    [HttpGet("vendor/{id:int}")]
    public async Task<IActionResult> GetVendor(int id)
    {
        var spools = await _spools.GetAllAsync(ProjectId);
        var vendor = Vendors(spools).FirstOrDefault(v => v.Id == id);
        return vendor == null ? SpoolmanNotFound("Vendor", id) : Ok(vendor);
    }

    // Everything else Spoolman offers (settings, fields, export, ...) is answered with an empty
    // object so clients that probe these endpoints keep working.
    [HttpGet("{**path}")]
    [HttpPost("{**path}")]
    [HttpPut("{**path}")]
    [HttpPatch("{**path}")]
    [HttpDelete("{**path}")]
    public IActionResult UnknownEndpoint() => Ok(new { });

    private static List<SpoolmanVendorResponse> Vendors(IEnumerable<Spool> spools) =>
        spools
            .Select(s => SpoolmanMapper.ToVendor(s.FilamentMaterial))
            .GroupBy(v => v.Id)
            .Select(g => g.First())
            .OrderBy(v => v.Name)
            .ToList();

    private NotFoundObjectResult SpoolmanNotFound(string resource, int id) =>
        NotFound(new { message = $"No {resource.ToLowerInvariant()} with ID {id} found." });
}

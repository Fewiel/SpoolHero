using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Spoolman;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/v1")]
public class SpoolmanController : ControllerBase
{
    private readonly ISpoolRepository _spools;

    public SpoolmanController(ISpoolRepository spools)
    {
        _spools = spools;
    }

    private Guid ProjectId => (Guid)HttpContext.Items["SpoolmanProjectId"]!;

    [HttpGet("health")]
    public IActionResult Health() => Ok(new SpoolmanHealthResponse());

    [HttpGet("info")]
    public IActionResult Info() => Ok(new SpoolmanInfoResponse());

    [HttpGet("spool/{id:int}")]
    [ServiceFilter(typeof(SpoolmanAuthFilter))]
    public async Task<IActionResult> GetSpool(int id)
    {
        var spool = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        if (spool == null) return NotFound();
        return Ok(MapToSpoolmanResponse(spool));
    }

    [HttpGet("spool")]
    [ServiceFilter(typeof(SpoolmanAuthFilter))]
    public async Task<IActionResult> ListSpools()
    {
        var spools = await _spools.GetAllByProjectAsync(ProjectId);
        return Ok(spools.Select(MapToSpoolmanResponse).ToList());
    }

    [HttpPut("spool/{id:int}/use")]
    [ServiceFilter(typeof(SpoolmanAuthFilter))]
    public async Task<IActionResult> UseSpool(int id, [FromBody] SpoolmanUseRequest request)
    {
        var spool = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        if (spool == null) return NotFound();

        var material = spool.FilamentMaterial;
        if (material == null) return StatusCode(500, new { message = "Filament material not found." });

        decimal subtractGrams;

        if (request.UseWeight.HasValue && request.UseWeight.Value > 0)
        {
            subtractGrams = request.UseWeight.Value;
        }
        else if (request.UseLength.HasValue && request.UseLength.Value > 0)
        {
            if (!material.DensityGCm3.HasValue || material.DensityGCm3.Value <= 0)
                return BadRequest(new { message = "Filament density not configured. Cannot convert length to weight." });

            var diameterMm = material.DiameterMm > 0 ? material.DiameterMm : 1.75m;
            var radiusCm = (diameterMm / 2m) / 10m;
            var lengthCm = request.UseLength.Value / 10m;
            var volumeCm3 = (decimal)Math.PI * radiusCm * radiusCm * lengthCm;
            subtractGrams = volumeCm3 * material.DensityGCm3.Value;
        }
        else
        {
            return BadRequest(new { message = "Either use_weight or use_length must be provided." });
        }

        var totalWeight = material.WeightGrams ?? 0;
        await _spools.UpdateRemainingWeightAtomicAsync(spool.Id, subtractGrams, totalWeight);

        var updated = await _spools.GetBySpoolmanIdAsync(id, ProjectId);
        return Ok(MapToSpoolmanResponse(updated!));
    }

    private static SpoolmanSpoolResponse MapToSpoolmanResponse(Spool spool)
    {
        var material = spool.FilamentMaterial;
        decimal totalWeight = material?.WeightGrams ?? 0;
        var remaining = spool.RemainingWeightGrams;
        var used = totalWeight - remaining;
        if (used < 0) used = 0;

        decimal remainingLength = 0;
        decimal usedLength = 0;

        if (material?.DensityGCm3 is > 0 && material.DiameterMm > 0)
        {
            var radiusCm = (material.DiameterMm / 2m) / 10m;
            var crossSectionCm2 = (decimal)Math.PI * radiusCm * radiusCm;
            var densityGPerCm3 = material.DensityGCm3.Value;

            if (crossSectionCm2 > 0 && densityGPerCm3 > 0)
            {
                remainingLength = (remaining / (crossSectionCm2 * densityGPerCm3)) * 10m;
                usedLength = (used / (crossSectionCm2 * densityGPerCm3)) * 10m;
            }
        }

        var vendorName = material?.Brand ?? "Unknown";
        var vendorId = vendorName.GetHashCode() & 0x7FFFFFFF;

        return new SpoolmanSpoolResponse
        {
            Id = spool.SpoolmanId,
            Registered = spool.CreatedAt.ToString("o"),
            FirstUsed = spool.OpenedAt?.ToString("o"),
            LastUsed = spool.UpdatedAt.ToString("o"),
            Filament = new SpoolmanFilamentResponse
            {
                Id = spool.SpoolmanId,
                Name = $"{material?.Brand ?? ""} {material?.Type ?? ""}".Trim(),
                Material = material?.Type ?? "",
                Vendor = new SpoolmanVendorResponse
                {
                    Id = vendorId,
                    Name = vendorName,
                },
                ColorHex = material?.ColorHex ?? "000000",
                Diameter = material?.DiameterMm ?? 1.75m,
                Density = material?.DensityGCm3 ?? 0,
                Weight = totalWeight,
                SpoolWeight = 0,
                SettingsExtruderTemp = material?.MaxTempCelsius ?? 0,
                SettingsBedTemp = material?.BedTempCelsius ?? 0,
            },
            RemainingWeight = remaining,
            UsedWeight = used,
            RemainingLength = Math.Round(remainingLength, 1),
            UsedLength = Math.Round(usedLength, 1),
            Archived = spool.ConsumedAt != null,
            Extra = new Dictionary<string, string>
            {
                ["spoolhero_id"] = spool.Id.ToString(),
            },
        };
    }
}

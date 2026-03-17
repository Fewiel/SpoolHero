using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Import;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class CsvImportController : ControllerBase
{
    private readonly IMaterialRepository _materials;
    private readonly ISpoolRepository _spools;
    private readonly IAuditService _audit;
    private readonly SpoolManagerDb _db;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public CsvImportController(IMaterialRepository materials, ISpoolRepository spools, IAuditService audit, SpoolManagerDb db)
    {
        _materials = materials;
        _spools = spools;
        _audit = audit;
        _db = db;
    }

    private const int MaxImportRows = 10_000;

    [HttpPost("csv")]
    public async Task<IActionResult> ImportCsv([FromBody] CsvImportRequest request)
    {
        if (request.Rows.Count > MaxImportRows)
            return BadRequest($"Too many rows ({request.Rows.Count}). Maximum is {MaxImportRows}.");

        var result = new CsvImportResult();
        var existingMaterials = await _materials.GetAllAsync(ProjectMember.ProjectId);
        var materialCache = new Dictionary<string, FilamentMaterial>();

        await using var transaction = await _db.BeginTransactionAsync();

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Brand) || string.IsNullOrWhiteSpace(row.Type))
            {
                result.Skipped++;
                continue;
            }

            var key = $"{row.Brand}|{row.Type}|{row.ColorHex ?? ""}|{row.DiameterMm ?? 1.75m}";

            if (!materialCache.TryGetValue(key, out var material))
            {
                material = existingMaterials.FirstOrDefault(m =>
                    string.Equals(m.Brand, row.Brand, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Type, row.Type, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(row.ColorHex) || string.Equals(m.ColorHex, row.ColorHex, StringComparison.OrdinalIgnoreCase)) &&
                    m.DiameterMm == (row.DiameterMm ?? 1.75m));

                if (material == null)
                {
                    material = new FilamentMaterial
                    {
                        ProjectId = ProjectMember.ProjectId,
                        Brand = row.Brand,
                        Type = row.Type,
                        ColorName = row.ColorName,
                        ColorHex = row.ColorHex?.TrimStart('#') ?? "CCCCCC",
                        MinTempCelsius = row.MinTempCelsius ?? 0,
                        MaxTempCelsius = row.MaxTempCelsius ?? 0,
                        BedTempCelsius = row.BedTempCelsius,
                        DiameterMm = row.DiameterMm ?? 1.75m,
                        WeightGrams = row.WeightGrams,
                        DensityGCm3 = row.DensityGCm3,
                        DryTempCelsius = row.DryTempCelsius,
                        DryTimeHours = row.DryTimeHours,
                        PricePerKg = row.PricePerKg,
                        ReorderUrl = MaterialsController.SanitizeUrl(row.ReorderUrl),
                        Notes = row.Notes,
                        Source = "csv"
                    };
                    var id = await _materials.CreateAsync(material);
                    material.Id = id;
                    result.MaterialsCreated++;
                }

                materialCache[key] = material;
            }

            if (request.CreateSpools)
            {
                var spool = new Spool
                {
                    ProjectId = ProjectMember.ProjectId,
                    FilamentMaterialId = material.Id,
                    RemainingWeightGrams = row.RemainingWeightGrams ?? (row.WeightGrams ?? 1000),
                    RemainingPercent = row.RemainingPercent ?? 100,
                    PurchasePrice = row.PurchasePrice
                };
                await _spools.CreateAsync(spool);
                result.SpoolsCreated++;
            }
        }

        await transaction.CommitAsync();

        await _audit.LogAsync("import.csv",
            userId: UserId, username: UserName,
            details: $"Imported {result.MaterialsCreated} materials, {result.SpoolsCreated} spools via CSV",
            projectId: ProjectMember.ProjectId,
            ipAddress: ClientIp);

        return Ok(result);
    }
}

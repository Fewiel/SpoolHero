using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Tags;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class InventoryController : ControllerBase
{
    private readonly ISpoolRepository _spools;
    private readonly IPrinterRepository _printers;
    private readonly IStorageLocationRepository _storageLocations;
    private readonly IDryerRepository _dryers;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    public InventoryController(ISpoolRepository spools, IPrinterRepository printers, IStorageLocationRepository storageLocations, IDryerRepository dryers)
    {
        _spools = spools;
        _printers = printers;
        _storageLocations = storageLocations;
        _dryers = dryers;
    }

    [HttpPost("identify")]
    public async Task<IActionResult> Identify(InventoryIdentifyRequest request)
    {
        var projectId = ProjectMember.ProjectId;

        if (!string.IsNullOrEmpty(request.JsonPayload))
        {
            try
            {
                var doc = JsonDocument.Parse(request.JsonPayload);
                var root = doc.RootElement;

                if (root.TryGetProperty("protocol", out var protocolEl))
                {
                    var protocol = protocolEl.GetString();

                    if (protocol == "spoolmanager" &&
                        root.TryGetProperty("type", out var typeEl) &&
                        root.TryGetProperty("id", out var idEl))
                    {
                        var entityType = typeEl.GetString() ?? string.Empty;
                        if (!Guid.TryParse(idEl.GetString(), out var entityId))
                            return Ok(new InventoryIdentifyResult { EntityType = "unknown", EntityId = Guid.Empty, EntityName = string.Empty });

                        if (entityType == "printer")
                        {
                            var printer = await _printers.GetByIdAsync(entityId);
                            if (printer != null && printer.ProjectId == projectId)
                                return Ok(new InventoryIdentifyResult { EntityType = "printer", EntityId = printer.Id, EntityName = printer.Name });
                        }
                        else if (entityType == "storage")
                        {
                            var storage = await _storageLocations.GetByIdAsync(entityId);
                            if (storage != null && storage.ProjectId == projectId)
                                return Ok(new InventoryIdentifyResult { EntityType = "storage", EntityId = storage.Id, EntityName = storage.Name });
                        }
                        else if (entityType == "dryer")
                        {
                            var dryer = await _dryers.GetByIdAsync(entityId);
                            if (dryer != null && dryer.ProjectId == projectId)
                                return Ok(new InventoryIdentifyResult { EntityType = "dryer", EntityId = dryer.Id, EntityName = dryer.Name });
                        }

                        return Ok(new InventoryIdentifyResult { EntityType = "unknown", EntityId = Guid.Empty, EntityName = string.Empty });
                    }

                    if (protocol == "openspool" &&
                        root.TryGetProperty("_sm_spool_id", out var spoolIdEl) &&
                        spoolIdEl.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(spoolIdEl.GetString(), out var parsedSpoolId))
                    {
                        var spool = await _spools.GetByIdAsync(parsedSpoolId, projectId);
                        if (spool != null)
                        {
                            var name = spool.FilamentMaterial != null
                                ? $"{spool.FilamentMaterial.Brand} {spool.FilamentMaterial.Type}"
                                : $"Spule #{parsedSpoolId}";
                            return Ok(new InventoryIdentifyResult { EntityType = "spool", EntityId = spool.Id, EntityName = name });
                        }
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(request.SerialNumber))
        {
            var spoolList = await _spools.GetAllAsync(projectId);
            var spool = spoolList.FirstOrDefault(s => s.RfidTagUid == request.SerialNumber);
            if (spool != null)
            {
                var name = spool.FilamentMaterial != null
                    ? $"{spool.FilamentMaterial.Brand} {spool.FilamentMaterial.Type}"
                    : $"Spule #{spool.Id}";
                return Ok(new InventoryIdentifyResult { EntityType = "spool", EntityId = spool.Id, EntityName = name });
            }

            var printerList = await _printers.GetAllAsync(projectId);
            var printer = printerList.FirstOrDefault(p => p.RfidTagUid == request.SerialNumber);
            if (printer != null)
                return Ok(new InventoryIdentifyResult { EntityType = "printer", EntityId = printer.Id, EntityName = printer.Name });

            var storageList = await _storageLocations.GetAllAsync(projectId);
            var storage = storageList.FirstOrDefault(s => s.RfidTagUid == request.SerialNumber);
            if (storage != null)
                return Ok(new InventoryIdentifyResult { EntityType = "storage", EntityId = storage.Id, EntityName = storage.Name });

            var dryerList = await _dryers.GetAllAsync(projectId);
            var dryer = dryerList.FirstOrDefault(d => d.RfidTagUid == request.SerialNumber);
            if (dryer != null)
                return Ok(new InventoryIdentifyResult { EntityType = "dryer", EntityId = dryer.Id, EntityName = dryer.Name });
        }

        return Ok(new InventoryIdentifyResult { EntityType = "unknown", EntityId = Guid.Empty, EntityName = string.Empty });
    }

    [HttpPost("action")]
    public async Task<IActionResult> PerformAction(InventoryActionRequest request)
    {
        var first = request.First;
        var second = request.Second;
        var projectId = ProjectMember.ProjectId;

        var validLocations = new[] { "printer", "storage", "dryer" };

        if (first.EntityType == "spool" && validLocations.Contains(second.EntityType))
        {
            var spool = await _spools.GetByIdAsync(first.EntityId, projectId);
            if (spool == null) return NotFound(new InventoryActionResult { Success = false, Description = "Spule nicht gefunden." });

            spool.PrinterId = null;
            spool.StorageLocationId = null;
            spool.DryerId = null;

            if (second.EntityType == "printer")
            {
                var printer = await _printers.GetByIdAsync(second.EntityId);
                if (printer == null || printer.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Drucker nicht gefunden." });
                spool.PrinterId = second.EntityId;
            }
            else if (second.EntityType == "storage")
            {
                var storage = await _storageLocations.GetByIdAsync(second.EntityId);
                if (storage == null || storage.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Lagerort nicht gefunden." });
                spool.StorageLocationId = second.EntityId;
            }
            else if (second.EntityType == "dryer")
            {
                var dryer = await _dryers.GetByIdAsync(second.EntityId);
                if (dryer == null || dryer.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Trockner nicht gefunden." });
                spool.DryerId = second.EntityId;
            }

            spool.UpdatedAt = DateTime.UtcNow;
            await _spools.UpdateAsync(spool);
            return Ok(new InventoryActionResult { Success = true, Description = $"Spule '{first.EntityName}' → {second.EntityName}" });
        }

        if (validLocations.Contains(first.EntityType) && second.EntityType == "spool")
        {
            var spool = await _spools.GetByIdAsync(second.EntityId, projectId);
            if (spool == null) return NotFound(new InventoryActionResult { Success = false, Description = "Spule nicht gefunden." });

            spool.PrinterId = null;
            spool.StorageLocationId = null;
            spool.DryerId = null;

            if (first.EntityType == "printer")
            {
                var printer = await _printers.GetByIdAsync(first.EntityId);
                if (printer == null || printer.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Drucker nicht gefunden." });
                spool.PrinterId = first.EntityId;
            }
            else if (first.EntityType == "storage")
            {
                var storage = await _storageLocations.GetByIdAsync(first.EntityId);
                if (storage == null || storage.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Lagerort nicht gefunden." });
                spool.StorageLocationId = first.EntityId;
            }
            else if (first.EntityType == "dryer")
            {
                var dryer = await _dryers.GetByIdAsync(first.EntityId);
                if (dryer == null || dryer.ProjectId != projectId) return NotFound(new InventoryActionResult { Success = false, Description = "Trockner nicht gefunden." });
                spool.DryerId = first.EntityId;
            }

            spool.UpdatedAt = DateTime.UtcNow;
            await _spools.UpdateAsync(spool);
            return Ok(new InventoryActionResult { Success = true, Description = $"Spule '{second.EntityName}' → {first.EntityName}" });
        }

        return BadRequest(new InventoryActionResult { Success = false, Description = "Keine gültige Kombination. Scanne Spule + Drucker/Lagerort/Trockner oder umgekehrt." });
    }
}

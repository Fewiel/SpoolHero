using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Tags;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class TagsController : ControllerBase
{
    private readonly IOpenSpoolService _openSpool;
    private readonly ISpoolRepository _spools;
    private readonly IMaterialRepository _materials;
    private readonly IPrinterRepository _printers;
    private readonly IStorageLocationRepository _storageLocations;
    private readonly IDryerRepository _dryers;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    private static readonly string[] ValidEntityTypes = ["spool", "printer", "storage", "dryer"];

    public TagsController(IOpenSpoolService openSpool, ISpoolRepository spools, IMaterialRepository materials,
        IPrinterRepository printers, IStorageLocationRepository storageLocations, IDryerRepository dryers)
    {
        _openSpool = openSpool;
        _spools = spools;
        _materials = materials;
        _printers = printers;
        _storageLocations = storageLocations;
        _dryers = dryers;
    }

    [HttpPost("encode")]
    public async Task<IActionResult> Encode(TagEncodeRequest request)
    {
        if (request.SpoolId.HasValue)
        {
            var spool = await _spools.GetByIdAsync(request.SpoolId.Value, ProjectMember.ProjectId);
            if (spool?.FilamentMaterial == null) return NotFound();

            var ndefBytes = _openSpool.Encode(spool.FilamentMaterial, spool.Id, spool.SpoolmanId);
            var json = _openSpool.ToJson(spool.FilamentMaterial, spool.Id);
            return Ok(new TagEncodeResponse { Base64 = Convert.ToBase64String(ndefBytes), JsonPayload = json });
        }
        else if (request.MaterialId.HasValue)
        {
            var material = await _materials.GetByIdAsync(request.MaterialId.Value);
            if (material == null) return NotFound();
            if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();

            var ndefBytes = _openSpool.Encode(material);
            var json = _openSpool.ToJson(material);
            return Ok(new TagEncodeResponse { Base64 = Convert.ToBase64String(ndefBytes), JsonPayload = json });
        }

        return BadRequest(new { message = "Provide spoolId or materialId." });
    }

    [HttpPost("encode-entity")]
    public async Task<IActionResult> EncodeEntity(TagEncodeEntityRequest request)
    {
        if (string.IsNullOrEmpty(request.EntityType) || request.EntityId == Guid.Empty)
            return BadRequest(new { message = "Provide entityType and entityId." });

        if (!ValidEntityTypes.Contains(request.EntityType))
            return BadRequest(new { message = "Invalid entity type." });

        var projectId = ProjectMember.ProjectId;
        switch (request.EntityType)
        {
            case "spool":
                var spool = await _spools.GetByIdAsync(request.EntityId, projectId);
                if (spool == null) return NotFound();
                break;
            case "printer":
                var printer = await _printers.GetByIdAsync(request.EntityId);
                if (printer == null || printer.ProjectId != projectId) return NotFound();
                break;
            case "storage":
                var storage = await _storageLocations.GetByIdAsync(request.EntityId);
                if (storage == null || storage.ProjectId != projectId) return NotFound();
                break;
            case "dryer":
                var dryer = await _dryers.GetByIdAsync(request.EntityId);
                if (dryer == null || dryer.ProjectId != projectId) return NotFound();
                break;
        }

        var json = $"{{\"protocol\":\"spoolmanager\",\"type\":\"{request.EntityType}\",\"id\":\"{request.EntityId}\"}}";
        var ndefBytes = _openSpool.EncodeEntityTag(request.EntityType, request.EntityId);
        return Ok(new TagEncodeResponse { Base64 = Convert.ToBase64String(ndefBytes), JsonPayload = json });
    }

    [HttpPost("decode")]
    public IActionResult Decode(TagDecodeRequest request)
    {
        try
        {
            var bytes = Convert.FromBase64String(request.Base64);
            var (material, isValid) = _openSpool.Decode(bytes);

            string rawJson = string.Empty;
            Guid? spoolId = null;
            if (isValid)
            {
                var json = _openSpool.ToJson(material);
                var (_, _, rj, sid) = _openSpool.FromJson(json);
                rawJson = rj;
                spoolId = sid;
            }

            return Ok(new TagDecodeResponse
            {
                RawJson = rawJson,
                Type = isValid ? material.Type : null,
                ColorHex = isValid ? material.ColorHex : null,
                Brand = isValid ? material.Brand : null,
                MinTemp = isValid ? material.MinTempCelsius : null,
                MaxTemp = isValid ? material.MaxTempCelsius : null,
                IsValid = isValid,
                SpoolId = spoolId
            });
        }
        catch
        {
            return BadRequest(new { message = "Invalid NDEF data." });
        }
    }

    [HttpGet("download/{spoolId}")]
    public async Task<IActionResult> Download(Guid spoolId)
    {
        var spool = await _spools.GetByIdAsync(spoolId, ProjectMember.ProjectId);
        if (spool?.FilamentMaterial == null) return NotFound();

        var bytes = _openSpool.Encode(spool.FilamentMaterial, spool.Id, spool.SpoolmanId);
        var filename = $"openspool_{spool.FilamentMaterial.Brand}_{spool.FilamentMaterial.Type}_{spoolId}.bin".Replace(" ", "_");
        return File(bytes, "application/octet-stream", filename);
    }
}

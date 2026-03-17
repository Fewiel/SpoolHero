using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Spools;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/spools")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class SpoolsController : ControllerBase
{
    private readonly ISpoolRepository _spools;
    private readonly IImageService _images;
    private readonly IAuditService _audit;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public SpoolsController(ISpoolRepository spools, IImageService images, IAuditService audit)
    {
        _spools = spools;
        _images = images;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? materialId,
        [FromQuery] Guid? printerId,
        [FromQuery] Guid? storageId,
        [FromQuery] Guid? dryerId,
        [FromQuery] bool? consumed,
        [FromQuery] string? search)
    {
        var spools = await _spools.GetAllAsync(ProjectMember.ProjectId, materialId, printerId, storageId, dryerId, consumed, search);
        return Ok(spools.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        return spool == null ? NotFound() : Ok(MapToDto(spool));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSpoolRequest request)
    {
        var spool = new Spool
        {
            ProjectId = ProjectMember.ProjectId,
            FilamentMaterialId = request.FilamentMaterialId,
            RfidTagUid = request.RfidTagUid,
            RemainingWeightGrams = request.RemainingWeightGrams,
            RemainingPercent = request.RemainingPercent,
            PrinterId = request.PrinterId,
            StorageLocationId = request.StorageLocationId,
            DryerId = request.DryerId,
            PurchasedAt = request.PurchasedAt,
            PurchasePrice = request.PurchasePrice,
            ReorderUrl = MaterialsController.SanitizeUrl(request.ReorderUrl),
            Notes = request.Notes
        };

        var id = await _spools.CreateAsync(spool);
        var created = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        return CreatedAtAction(nameof(GetById), new { id }, MapToDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSpoolRequest request)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();

        spool.FilamentMaterialId = request.FilamentMaterialId;
        spool.RemainingWeightGrams = request.RemainingWeightGrams;
        spool.RemainingPercent = request.RemainingPercent;
        spool.RfidTagUid = request.RfidTagUid;
        spool.PrinterId = request.PrinterId;
        spool.StorageLocationId = request.StorageLocationId;
        spool.DryerId = request.DryerId;
        spool.PurchasedAt = request.PurchasedAt;
        spool.PurchasePrice = request.PurchasePrice;
        spool.ReorderUrl = MaterialsController.SanitizeUrl(request.ReorderUrl);
        spool.Notes = request.Notes;
        spool.UpdatedAt = DateTime.UtcNow;

        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();

        var name = spool.FilamentMaterial != null ? $"{spool.FilamentMaterial.Brand} {spool.FilamentMaterial.Type}" : $"Spool #{id}";
        await _audit.LogAsync("spool.delete",
            userId: UserId, username: UserName,
            entityType: "spool", entityId: id.ToString(), entityName: name,
            projectId: ProjectMember.ProjectId,
            ipAddress: ClientIp);

        await _spools.DeleteAsync(id);
        return NoContent();
    }

    [HttpPut("{id}/open")]
    public async Task<IActionResult> MarkOpened(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.OpenedAt = DateTime.UtcNow;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPut("{id}/dry")]
    public async Task<IActionResult> MarkDried(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.DriedAt = DateTime.UtcNow;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPut("{id}/repackage")]
    public async Task<IActionResult> MarkRepackaged(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.RepackagedAt = DateTime.UtcNow;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPut("{id}/reopen")]
    public async Task<IActionResult> MarkReopened(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.ReopenedAt = DateTime.UtcNow;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPut("{id}/consume")]
    public async Task<IActionResult> MarkConsumed(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.ConsumedAt = DateTime.UtcNow;
        spool.RemainingWeightGrams = 0;
        spool.RemainingPercent = 0;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);

        var name = spool.FilamentMaterial != null ? $"{spool.FilamentMaterial.Brand} {spool.FilamentMaterial.Type}" : $"Spool #{id}";
        await _audit.LogAsync("spool.consume",
            userId: UserId, username: UserName,
            entityType: "spool", entityId: id.ToString(), entityName: name,
            projectId: ProjectMember.ProjectId,
            ipAddress: ClientIp);

        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPatch("{id}/remaining")]
    public async Task<IActionResult> UpdateRemaining(Guid id, UpdateRemainingRequest request)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.RemainingWeightGrams = request.RemainingWeightGrams;
        spool.RemainingPercent = request.RemainingPercent;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(MapToDto((await _spools.GetByIdAsync(id, ProjectMember.ProjectId))!));
    }

    [HttpPost("{id}/image")]
    [RequestSizeLimit(8_388_608)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType)) return BadRequest(new { message = "Unsupported image type." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        spool.ImageData = _images.ResizeToThumbnail(ms.ToArray());
        spool.ImageContentType = "image/jpeg";
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return Ok(new { imageBase64 = Convert.ToBase64String(spool.ImageData), imageContentType = spool.ImageContentType });
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var spool = await _spools.GetByIdAsync(id, ProjectMember.ProjectId);
        if (spool == null) return NotFound();
        spool.ImageData = null;
        spool.ImageContentType = null;
        spool.UpdatedAt = DateTime.UtcNow;
        await _spools.UpdateAsync(spool);
        return NoContent();
    }

    private static SpoolDto MapToDto(Spool s) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        FilamentMaterialId = s.FilamentMaterialId,
        MaterialType = s.FilamentMaterial?.Type ?? string.Empty,
        MaterialBrand = s.FilamentMaterial?.Brand ?? string.Empty,
        MaterialColorHex = s.FilamentMaterial?.ColorHex ?? "FFFFFF",
        MaterialColorName = s.FilamentMaterial?.ColorName,
        RfidTagUid = s.RfidTagUid,
        OpenedAt = s.OpenedAt,
        RepackagedAt = s.RepackagedAt,
        ReopenedAt = s.ReopenedAt,
        DriedAt = s.DriedAt,
        ConsumedAt = s.ConsumedAt,
        RemainingWeightGrams = s.RemainingWeightGrams,
        RemainingPercent = s.RemainingPercent,
        PrinterId = s.PrinterId,
        PrinterName = s.Printer?.Name,
        StorageLocationId = s.StorageLocationId,
        StorageLocationName = s.StorageLocation?.Name,
        DryerId = s.DryerId,
        DryerName = s.Dryer?.Name,
        MaterialDryTimeHours = s.FilamentMaterial?.DryTimeHours,
        PurchasedAt = s.PurchasedAt,
        PurchasePrice = s.PurchasePrice,
        ReorderUrl = s.ReorderUrl,
        Notes = s.Notes,
        ImageBase64 = s.ImageData != null ? Convert.ToBase64String(s.ImageData) : null,
        ImageContentType = s.ImageContentType,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}

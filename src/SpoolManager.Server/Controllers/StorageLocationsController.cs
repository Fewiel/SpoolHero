using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Storage;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/storage-locations")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class StorageLocationsController : ControllerBase
{
    private readonly IStorageLocationRepository _storage;
    private readonly IImageService _images;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    public StorageLocationsController(IStorageLocationRepository storage, IImageService images)
    {
        _storage = storage;
        _images = images;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var locations = await _storage.GetAllAsync(ProjectMember.ProjectId);
        return Ok(locations.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var location = await _storage.GetByIdAsync(id);
        if (location == null || location.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        return Ok(MapToDto(location));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStorageLocationRequest request)
    {
        var location = new StorageLocation
        {
            ProjectId = ProjectMember.ProjectId,
            Name = request.Name,
            Description = request.Description,
            RfidTagUid = request.RfidTagUid
        };
        var id = await _storage.CreateAsync(location);
        return CreatedAtAction(nameof(GetById), new { id }, MapToDto((await _storage.GetByIdAsync(id))!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateStorageLocationRequest request)
    {
        var location = await _storage.GetByIdAsync(id);
        if (location == null || location.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        location.Name = request.Name;
        location.Description = request.Description;
        location.RfidTagUid = request.RfidTagUid;
        location.UpdatedAt = DateTime.UtcNow;
        await _storage.UpdateAsync(location);
        return Ok(MapToDto((await _storage.GetByIdAsync(id))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (ProjectMember.Role != "admin")
            return StatusCode(403, new { message = "Only admins can delete." });
        var location = await _storage.GetByIdAsync(id);
        if (location == null || location.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        await _storage.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/image")]
    [RequestSizeLimit(8_388_608)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var location = await _storage.GetByIdAsync(id);
        if (location == null || location.ProjectId != ProjectMember.ProjectId)
            return NotFound();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var (data, ct) = _images.ResizeToThumbnail(ms.ToArray(), file.ContentType);
        location.ImageData = data;
        location.ImageContentType = ct;
        location.UpdatedAt = DateTime.UtcNow;
        await _storage.UpdateAsync(location);
        return Ok(MapToDto(location));
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var location = await _storage.GetByIdAsync(id);
        if (location == null || location.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        location.ImageData = null;
        location.ImageContentType = null;
        location.UpdatedAt = DateTime.UtcNow;
        await _storage.UpdateAsync(location);
        return Ok(MapToDto(location));
    }

    private static StorageLocationDto MapToDto(StorageLocation sl) => new()
    {
        Id = sl.Id,
        ProjectId = sl.ProjectId,
        Name = sl.Name,
        Description = sl.Description,
        RfidTagUid = sl.RfidTagUid,
        ImageBase64 = sl.ImageData != null ? Convert.ToBase64String(sl.ImageData) : null,
        ImageContentType = sl.ImageContentType,
        CreatedAt = sl.CreatedAt,
        UpdatedAt = sl.UpdatedAt
    };
}

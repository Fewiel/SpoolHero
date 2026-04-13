using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/dryers")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class DryersController : ControllerBase
{
    private readonly IDryerRepository _dryers;
    private readonly IImageService _images;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    public DryersController(IDryerRepository dryers, IImageService images)
    {
        _dryers = dryers;
        _images = images;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var dryers = await _dryers.GetAllAsync(ProjectMember.ProjectId);
        return Ok(dryers.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dryer = await _dryers.GetByIdAsync(id);
        if (dryer == null || dryer.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        return Ok(MapToDto(dryer));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDryerRequest request)
    {
        var dryer = new Dryer
        {
            ProjectId = ProjectMember.ProjectId,
            Name = request.Name,
            Description = request.Description,
            RfidTagUid = request.RfidTagUid
        };
        var id = await _dryers.CreateAsync(dryer);
        return CreatedAtAction(nameof(GetById), new { id }, MapToDto((await _dryers.GetByIdAsync(id))!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateDryerRequest request)
    {
        var dryer = await _dryers.GetByIdAsync(id);
        if (dryer == null || dryer.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        dryer.Name = request.Name;
        dryer.Description = request.Description;
        dryer.RfidTagUid = request.RfidTagUid;
        dryer.UpdatedAt = DateTime.UtcNow;
        await _dryers.UpdateAsync(dryer);
        return Ok(MapToDto((await _dryers.GetByIdAsync(id))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (ProjectMember.Role != "admin")
            return StatusCode(403, new { message = "Only admins can delete." });
        var dryer = await _dryers.GetByIdAsync(id);
        if (dryer == null || dryer.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        await _dryers.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/image")]
    [RequestSizeLimit(8_388_608)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var dryer = await _dryers.GetByIdAsync(id);
        if (dryer == null || dryer.ProjectId != ProjectMember.ProjectId)
            return NotFound();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var (data, ct) = _images.ResizeToThumbnail(ms.ToArray(), file.ContentType);
        dryer.ImageData = data;
        dryer.ImageContentType = ct;
        dryer.UpdatedAt = DateTime.UtcNow;
        await _dryers.UpdateAsync(dryer);
        return Ok(MapToDto(dryer));
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var dryer = await _dryers.GetByIdAsync(id);
        if (dryer == null || dryer.ProjectId != ProjectMember.ProjectId)
            return NotFound();
        dryer.ImageData = null;
        dryer.ImageContentType = null;
        dryer.UpdatedAt = DateTime.UtcNow;
        await _dryers.UpdateAsync(dryer);
        return Ok(MapToDto(dryer));
    }

    private static DryerDto MapToDto(Dryer d) => new()
    {
        Id = d.Id,
        ProjectId = d.ProjectId,
        Name = d.Name,
        Description = d.Description,
        RfidTagUid = d.RfidTagUid,
        ImageBase64 = d.ImageData != null ? Convert.ToBase64String(d.ImageData) : null,
        ImageContentType = d.ImageContentType,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}

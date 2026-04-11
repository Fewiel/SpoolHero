using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/printers")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class PrintersController : ControllerBase
{
    private readonly IPrinterRepository _printers;
    private readonly IImageService _images;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    public PrintersController(IPrinterRepository printers, IImageService images)
    {
        _printers = printers;
        _images = images;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var printers = await _printers.GetAllAsync(ProjectMember.ProjectId);
        return Ok(printers.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var printer = await _printers.GetByIdAsync(id);
        if (printer == null || printer.ProjectId != ProjectMember.ProjectId) return NotFound();
        return Ok(MapToDto(printer));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePrinterRequest request)
    {
        var printer = new Printer
        {
            ProjectId = ProjectMember.ProjectId,
            Name = request.Name,
            Notes = request.Notes,
            RfidTagUid = request.RfidTagUid
        };
        var id = await _printers.CreateAsync(printer);
        return CreatedAtAction(nameof(GetById), new { id }, MapToDto((await _printers.GetByIdAsync(id))!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdatePrinterRequest request)
    {
        var printer = await _printers.GetByIdAsync(id);
        if (printer == null || printer.ProjectId != ProjectMember.ProjectId) return NotFound();
        printer.Name = request.Name;
        printer.Notes = request.Notes;
        printer.RfidTagUid = request.RfidTagUid;
        printer.UpdatedAt = DateTime.UtcNow;
        await _printers.UpdateAsync(printer);
        return Ok(MapToDto((await _printers.GetByIdAsync(id))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (ProjectMember.Role != "admin") return StatusCode(403, new { message = "Only admins can delete." });
        var printer = await _printers.GetByIdAsync(id);
        if (printer == null || printer.ProjectId != ProjectMember.ProjectId) return NotFound();
        await _printers.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/image")]
    [RequestSizeLimit(8_388_608)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var printer = await _printers.GetByIdAsync(id);
        if (printer == null || printer.ProjectId != ProjectMember.ProjectId) return NotFound();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var (data, ct) = _images.ResizeToThumbnail(ms.ToArray(), file.ContentType);
        printer.ImageData = data;
        printer.ImageContentType = ct;
        printer.UpdatedAt = DateTime.UtcNow;
        await _printers.UpdateAsync(printer);
        return Ok(MapToDto(printer));
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var printer = await _printers.GetByIdAsync(id);
        if (printer == null || printer.ProjectId != ProjectMember.ProjectId) return NotFound();
        printer.ImageData = null;
        printer.ImageContentType = null;
        printer.UpdatedAt = DateTime.UtcNow;
        await _printers.UpdateAsync(printer);
        return Ok(MapToDto(printer));
    }

    private static PrinterDto MapToDto(Printer p) => new()
    {
        Id = p.Id,
        ProjectId = p.ProjectId,
        Name = p.Name,
        Notes = p.Notes,
        RfidTagUid = p.RfidTagUid,
        ImageBase64 = p.ImageData != null ? Convert.ToBase64String(p.ImageData) : null,
        ImageContentType = p.ImageContentType,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}

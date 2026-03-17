using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialRepository _materials;
    private readonly IMaterialExportService _exportService;
    private readonly IImageService _images;
    private readonly IAuditService _audit;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public MaterialsController(IMaterialRepository materials, IMaterialExportService exportService,
        IImageService images, IAuditService audit)
    {
        _materials = materials;
        _exportService = exportService;
        _images = images;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var materials = await _materials.GetAllAsync(ProjectMember.ProjectId, search);
        return Ok(materials.Select(MapToDto));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] MaterialSearchRequest request)
    {
        var (items, totalCount, brands, types) = await _materials.SearchAsync(
            request.Query, request.MaterialType, request.Brand,
            request.GlobalOnly, request.Limit, request.Offset,
            ProjectMember.ProjectId);

        return Ok(new MaterialSearchResult
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            AvailableBrands = brands,
            AvailableTypes = types
        });
    }

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands()
    {
        var brands = await _materials.GetDistinctBrandsAsync(ProjectMember.ProjectId);
        return Ok(brands);
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetTypes()
    {
        var types = await _materials.GetDistinctTypesAsync(ProjectMember.ProjectId);
        return Ok(types);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();
        return Ok(MapToDto(material));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMaterialRequest request)
    {
        var material = MapFromRequest(request);
        material.ProjectId = ProjectMember.ProjectId;
        var id = await _materials.CreateAsync(material);
        var created = await _materials.GetByIdAsync(id);

        await _audit.LogAsync("material.create",
            userId: UserId, username: UserName,
            entityType: "material", entityId: id.ToString(), entityName: $"{request.Brand} {request.Type}",
            projectId: ProjectMember.ProjectId,
            ipAddress: ClientIp);

        return CreatedAtAction(nameof(GetById), new { id }, MapToDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateMaterialRequest request)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (material.ProjectId == null) return Forbid();
        if (material.ProjectId != ProjectMember.ProjectId) return NotFound();

        ApplyRequest(material, request);
        material.UpdatedAt = DateTime.UtcNow;
        await _materials.UpdateAsync(material);
        return Ok(MapToDto((await _materials.GetByIdAsync(id))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (material.ProjectId == null) return Forbid();
        if (material.ProjectId != ProjectMember.ProjectId) return NotFound();

        await _audit.LogAsync("material.delete",
            userId: UserId, username: UserName,
            entityType: "material", entityId: id.ToString(), entityName: $"{material.Brand} {material.Type}",
            projectId: ProjectMember.ProjectId,
            ipAddress: ClientIp);

        await _materials.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/click")]
    public async Task<IActionResult> TrackClick(Guid id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        await _materials.IncrementClickCountAsync(id);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? ids)
    {
        List<FilamentMaterial> materials;

        if (!string.IsNullOrWhiteSpace(ids))
        {
            var idList = new List<Guid>();
            foreach (var part in ids.Split(','))
            {
                if (!Guid.TryParse(part.Trim(), out var parsed))
                    return BadRequest(new { message = "Invalid id list." });
                idList.Add(parsed);
            }
            var all = await _materials.GetByIdsAsync(idList);
            materials = all.Where(m => m.ProjectId == null || m.ProjectId == ProjectMember.ProjectId).ToList();
        }
        else
        {
            materials = await _materials.GetAllAsync(ProjectMember.ProjectId);
        }

        var dtos = materials.Select(MapToDto).ToList();
        var base64 = _exportService.ExportToBase64(dtos);
        return Ok(new { base64 });
    }

    [HttpPost("{id}/image")]
    [RequestSizeLimit(8_388_608)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType)) return BadRequest(new { message = "Unsupported image type." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        material.ImageData = _images.ResizeToThumbnail(ms.ToArray());
        material.ImageContentType = "image/jpeg";
        material.UpdatedAt = DateTime.UtcNow;
        await _materials.UpdateAsync(material);
        return Ok(new { imageBase64 = Convert.ToBase64String(material.ImageData), imageContentType = material.ImageContentType });
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var material = await _materials.GetByIdAsync(id);
        if (material == null) return NotFound();
        if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();

        material.ImageData = null;
        material.ImageContentType = null;
        material.UpdatedAt = DateTime.UtcNow;
        await _materials.UpdateAsync(material);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request)
    {
        var (materials, error) = _exportService.ImportFromBase64(request.Base64);
        if (error != null) return BadRequest(new { message = error });

        var created = new List<Guid>();
        foreach (var dto in materials)
        {
            var material = MapFromDto(dto);
            material.ProjectId = ProjectMember.ProjectId;
            var id = await _materials.CreateAsync(material);
            created.Add(id);
        }

        return Ok(new { imported = created.Count });
    }

    internal static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
            return url;
        return null;
    }

    internal static FilamentMaterial MapFromRequest(CreateMaterialRequest r) => new()
    {
        Type = r.Type,
        ColorHex = r.ColorHex,
        Brand = r.Brand,
        MinTempCelsius = r.MinTempCelsius,
        MaxTempCelsius = r.MaxTempCelsius,
        ColorName = r.ColorName,
        DiameterMm = r.DiameterMm,
        WeightGrams = r.WeightGrams,
        BedTempCelsius = r.BedTempCelsius,
        DensityGCm3 = r.DensityGCm3,
        DryTempCelsius = r.DryTempCelsius,
        DryTimeHours = r.DryTimeHours,
        Notes = r.Notes,
        ReorderUrl = SanitizeUrl(r.ReorderUrl),
        PricePerKg = r.PricePerKg,
        IsPublic = r.IsPublic
    };

    internal static void ApplyRequest(FilamentMaterial m, CreateMaterialRequest r)
    {
        m.Type = r.Type;
        m.ColorHex = r.ColorHex;
        m.Brand = r.Brand;
        m.MinTempCelsius = r.MinTempCelsius;
        m.MaxTempCelsius = r.MaxTempCelsius;
        m.ColorName = r.ColorName;
        m.DiameterMm = r.DiameterMm;
        m.WeightGrams = r.WeightGrams;
        m.BedTempCelsius = r.BedTempCelsius;
        m.DensityGCm3 = r.DensityGCm3;
        m.DryTempCelsius = r.DryTempCelsius;
        m.DryTimeHours = r.DryTimeHours;
        m.Notes = r.Notes;
        m.ReorderUrl = SanitizeUrl(r.ReorderUrl);
        m.PricePerKg = r.PricePerKg;
        m.IsPublic = r.IsPublic;
    }

    internal static FilamentMaterialDto MapToDto(FilamentMaterial m) => new()
    {
        Id = m.Id, Type = m.Type, ColorHex = m.ColorHex, Brand = m.Brand,
        MinTempCelsius = m.MinTempCelsius, MaxTempCelsius = m.MaxTempCelsius,
        ColorName = m.ColorName, DiameterMm = m.DiameterMm, WeightGrams = m.WeightGrams,
        BedTempCelsius = m.BedTempCelsius, DensityGCm3 = m.DensityGCm3,
        DryTempCelsius = m.DryTempCelsius, DryTimeHours = m.DryTimeHours,
        Notes = m.Notes, ReorderUrl = m.ReorderUrl, PricePerKg = m.PricePerKg,
        IsPublic = m.IsPublic, ReorderClickCount = m.ReorderClickCount,
        Source = m.Source, ProductName = m.ProductName,
        SpoolWeightGrams = m.SpoolWeightGrams, SpoolType = m.SpoolType,
        Finish = m.Finish, Translucent = m.Translucent, Glow = m.Glow, Fill = m.Fill,
        ImageBase64 = m.ImageData != null ? Convert.ToBase64String(m.ImageData) : null,
        ImageContentType = m.ImageContentType,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };

    private static FilamentMaterial MapFromDto(FilamentMaterialDto d) => new()
    {
        Type = d.Type, ColorHex = d.ColorHex, Brand = d.Brand,
        MinTempCelsius = d.MinTempCelsius, MaxTempCelsius = d.MaxTempCelsius,
        ColorName = d.ColorName, DiameterMm = d.DiameterMm, WeightGrams = d.WeightGrams,
        BedTempCelsius = d.BedTempCelsius, DensityGCm3 = d.DensityGCm3,
        DryTempCelsius = d.DryTempCelsius, DryTimeHours = d.DryTimeHours,
        Notes = d.Notes, ReorderUrl = d.ReorderUrl, PricePerKg = d.PricePerKg,
        IsPublic = d.IsPublic, Source = d.Source, ProductName = d.ProductName,
        SpoolWeightGrams = d.SpoolWeightGrams, SpoolType = d.SpoolType,
        Finish = d.Finish, Translucent = d.Translucent, Glow = d.Glow, Fill = d.Fill
    };
}

public class ImportRequest
{
    public string Base64 { get; set; } = string.Empty;
}

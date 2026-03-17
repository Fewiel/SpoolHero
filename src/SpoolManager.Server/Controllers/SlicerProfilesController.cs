using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.SlicerProfiles;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/materials/{materialId}/slicer-profiles")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class SlicerProfilesController : ControllerBase
{
    private readonly ISlicerProfileRepository _profiles;
    private readonly IMaterialRepository _materials;
    private readonly IPrinterRepository _printers;
    private readonly ISlicerExportService _export;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;

    public SlicerProfilesController(ISlicerProfileRepository profiles, IMaterialRepository materials,
        IPrinterRepository printers, ISlicerExportService export)
    {
        _profiles = profiles;
        _materials = materials;
        _printers = printers;
        _export = export;
    }

    [HttpGet]
    public async Task<IActionResult> GetByMaterial(Guid materialId, [FromQuery] Guid? printerId)
    {
        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();
        if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();

        List<SlicerProfile> profiles;
        if (printerId.HasValue)
            profiles = await _profiles.GetByMaterialAndPrinterAsync(materialId, printerId.Value);
        else
            profiles = await _profiles.GetByMaterialAsync(materialId);

        var printerIds = profiles.Where(p => p.PrinterId.HasValue).Select(p => p.PrinterId!.Value).Distinct().ToList();
        var printers = printerIds.Count > 0 ? await _printers.GetByIdsAsync(printerIds) : [];
        var printerNames = printers.ToDictionary(p => p.Id, p => p.Name);

        return Ok(profiles.Select(p => MapToDto(p, printerNames)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid materialId, Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile == null || profile.FilamentMaterialId != materialId) return NotFound();
        if (profile.ProjectId != null && profile.ProjectId != ProjectMember.ProjectId) return NotFound();

        var printerNames = new Dictionary<Guid, string>();
        if (profile.PrinterId.HasValue)
        {
            var printer = await _printers.GetByIdAsync(profile.PrinterId.Value);
            if (printer != null) printerNames[printer.Id] = printer.Name;
        }

        return Ok(MapToDto(profile, printerNames));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid materialId, CreateSlicerProfileRequest request)
    {
        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();
        if (material.ProjectId != null && material.ProjectId != ProjectMember.ProjectId) return NotFound();

        var profile = MapFromRequest(request);
        profile.FilamentMaterialId = materialId;
        profile.ProjectId = ProjectMember.ProjectId;

        var id = await _profiles.CreateAsync(profile);
        return CreatedAtAction(nameof(GetById), new { materialId, id }, null);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid materialId, Guid id, UpdateSlicerProfileRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile == null || profile.FilamentMaterialId != materialId) return NotFound();
        if (profile.ProjectId != ProjectMember.ProjectId) return Forbid();

        ApplyRequest(profile, request);
        await _profiles.UpdateAsync(profile);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid materialId, Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile == null || profile.FilamentMaterialId != materialId) return NotFound();
        if (profile.ProjectId != ProjectMember.ProjectId) return Forbid();

        await _profiles.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(Guid materialId, Guid id, [FromQuery] string format = "orcaslicer")
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile == null || profile.FilamentMaterialId != materialId) return NotFound();
        if (profile.ProjectId != null && profile.ProjectId != ProjectMember.ProjectId) return NotFound();

        var material = await _materials.GetByIdAsync(materialId);
        if (material == null) return NotFound();

        string? printerName = null;
        if (profile.PrinterId.HasValue)
        {
            var printer = await _printers.GetByIdAsync(profile.PrinterId.Value);
            printerName = printer?.Name;
        }

        string content;
        string fileName;
        string contentType;
        var safeName = SanitizeFileName(profile.Name);

        if (format.Equals("prusaslicer", StringComparison.OrdinalIgnoreCase))
        {
            content = _export.ExportToPrusaSlicer(profile, material, printerName);
            fileName = $"{safeName}.ini";
            contentType = "text/plain";
        }
        else
        {
            content = _export.ExportToOrcaSlicer(profile, material, printerName);
            fileName = $"{safeName}.json";
            contentType = "application/json";
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, contentType, fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "profile" : sanitized;
    }

    private static SlicerProfileDto MapToDto(SlicerProfile p, Dictionary<Guid, string> printerNames) => new()
    {
        Id = p.Id,
        FilamentMaterialId = p.FilamentMaterialId,
        PrinterId = p.PrinterId,
        PrinterName = p.PrinterId.HasValue && printerNames.TryGetValue(p.PrinterId.Value, out var name) ? name : null,
        Name = p.Name,
        SlicerType = p.SlicerType,
        NozzleTemp = p.NozzleTemp,
        NozzleTempInitialLayer = p.NozzleTempInitialLayer,
        BedTemp = p.BedTemp,
        BedTempInitialLayer = p.BedTempInitialLayer,
        ChamberTemp = p.ChamberTemp,
        MaxVolumetricSpeed = p.MaxVolumetricSpeed,
        FilamentFlowRatio = p.FilamentFlowRatio,
        PressureAdvance = p.PressureAdvance,
        RetractionLength = p.RetractionLength,
        RetractionSpeed = p.RetractionSpeed,
        ZHop = p.ZHop,
        FanMinSpeed = p.FanMinSpeed,
        FanMaxSpeed = p.FanMaxSpeed,
        FanDisableFirstLayers = p.FanDisableFirstLayers,
        OverhangFanSpeed = p.OverhangFanSpeed,
        FilamentStartGcode = p.FilamentStartGcode,
        FilamentEndGcode = p.FilamentEndGcode,
        Notes = p.Notes,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static SlicerProfile MapFromRequest(CreateSlicerProfileRequest r) => new()
    {
        PrinterId = r.PrinterId,
        Name = r.Name,
        SlicerType = r.SlicerType,
        NozzleTemp = r.NozzleTemp,
        NozzleTempInitialLayer = r.NozzleTempInitialLayer,
        BedTemp = r.BedTemp,
        BedTempInitialLayer = r.BedTempInitialLayer,
        ChamberTemp = r.ChamberTemp,
        MaxVolumetricSpeed = r.MaxVolumetricSpeed,
        FilamentFlowRatio = r.FilamentFlowRatio,
        PressureAdvance = r.PressureAdvance,
        RetractionLength = r.RetractionLength,
        RetractionSpeed = r.RetractionSpeed,
        ZHop = r.ZHop,
        FanMinSpeed = r.FanMinSpeed,
        FanMaxSpeed = r.FanMaxSpeed,
        FanDisableFirstLayers = r.FanDisableFirstLayers,
        OverhangFanSpeed = r.OverhangFanSpeed,
        FilamentStartGcode = r.FilamentStartGcode,
        FilamentEndGcode = r.FilamentEndGcode,
        Notes = r.Notes
    };

    private static void ApplyRequest(SlicerProfile p, CreateSlicerProfileRequest r)
    {
        p.PrinterId = r.PrinterId;
        p.Name = r.Name;
        p.SlicerType = r.SlicerType;
        p.NozzleTemp = r.NozzleTemp;
        p.NozzleTempInitialLayer = r.NozzleTempInitialLayer;
        p.BedTemp = r.BedTemp;
        p.BedTempInitialLayer = r.BedTempInitialLayer;
        p.ChamberTemp = r.ChamberTemp;
        p.MaxVolumetricSpeed = r.MaxVolumetricSpeed;
        p.FilamentFlowRatio = r.FilamentFlowRatio;
        p.PressureAdvance = r.PressureAdvance;
        p.RetractionLength = r.RetractionLength;
        p.RetractionSpeed = r.RetractionSpeed;
        p.ZHop = r.ZHop;
        p.FanMinSpeed = r.FanMinSpeed;
        p.FanMaxSpeed = r.FanMaxSpeed;
        p.FanDisableFirstLayers = r.FanDisableFirstLayers;
        p.OverhangFanSpeed = r.OverhangFanSpeed;
        p.FilamentStartGcode = r.FilamentStartGcode;
        p.FilamentEndGcode = r.FilamentEndGcode;
        p.Notes = r.Notes;
    }
}

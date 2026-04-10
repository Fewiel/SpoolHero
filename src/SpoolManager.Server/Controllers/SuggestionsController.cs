using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Suggestions;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/suggestions")]
[Authorize]
public class SuggestionsController : ControllerBase
{
    private readonly ISuggestionRepository _suggestions;
    private readonly IMaterialRepository _materials;
    private readonly IAuditService _audit;

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public SuggestionsController(ISuggestionRepository suggestions, IMaterialRepository materials, IAuditService audit)
    {
        _suggestions = suggestions;
        _materials = materials;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetMySuggestions()
    {
        var suggestions = await _suggestions.GetByUserAsync(UserId);
        var result = new List<MaterialSuggestionDto>();
        foreach (var s in suggestions)
        {
            string? materialName = null;
            if (s.MaterialId.HasValue)
            {
                var mat = await _materials.GetByIdAsync(s.MaterialId.Value);
                if (mat != null) materialName = $"{mat.Brand} {mat.Type}";
            }
            result.Add(MapToDto(s, materialName));
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSuggestionRequest request)
    {
        var suggestion = new MaterialSuggestion
        {
            MaterialId = request.MaterialId,
            UserId = UserId,
            Username = UserName ?? "Unknown",
            Type = request.Type,
            Brand = request.Brand,
            ColorHex = request.ColorHex,
            ColorName = request.ColorName,
            MinTempCelsius = request.MinTempCelsius,
            MaxTempCelsius = request.MaxTempCelsius,
            BedTempCelsius = request.BedTempCelsius,
            DiameterMm = request.DiameterMm,
            DensityGCm3 = request.DensityGCm3,
            DryTempCelsius = request.DryTempCelsius,
            DryTimeHours = request.DryTimeHours,
            Notes = request.Notes,
            ReorderUrl = request.ReorderUrl,
            PricePerKg = request.PricePerKg,
            Status = MaterialSuggestion.StatusPending
        };

        var id = await _suggestions.CreateAsync(suggestion);

        await _audit.LogAsync("suggestion.create", userId: UserId, username: UserName,
            entityType: "suggestion", entityId: id.ToString(),
            entityName: $"{request.Brand} {request.Type}",
            ipAddress: ClientIp);

        return Ok(new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var suggestion = await _suggestions.GetByIdAsync(id);
        if (suggestion == null) return NotFound();
        if (suggestion.UserId != UserId) return NotFound();

        string? materialName = null;
        if (suggestion.MaterialId.HasValue)
        {
            var mat = await _materials.GetByIdAsync(suggestion.MaterialId.Value);
            if (mat != null) materialName = $"{mat.Brand} {mat.Type}";
        }

        return Ok(MapToDto(suggestion, materialName));
    }

    private static MaterialSuggestionDto MapToDto(MaterialSuggestion s, string? materialName) => new()
    {
        Id = s.Id, MaterialId = s.MaterialId, MaterialName = materialName,
        UserId = s.UserId, Username = s.Username,
        Type = s.Type, Brand = s.Brand, ColorHex = s.ColorHex, ColorName = s.ColorName,
        MinTempCelsius = s.MinTempCelsius, MaxTempCelsius = s.MaxTempCelsius,
        BedTempCelsius = s.BedTempCelsius, DiameterMm = s.DiameterMm,
        DensityGCm3 = s.DensityGCm3, DryTempCelsius = s.DryTempCelsius,
        DryTimeHours = s.DryTimeHours, Notes = s.Notes,
        ReorderUrl = s.ReorderUrl, PricePerKg = s.PricePerKg,
        Status = s.Status, AdminNotes = s.AdminNotes,
        CreatedAt = s.CreatedAt, ReviewedAt = s.ReviewedAt
    };
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Admin;
using SpoolManager.Shared.DTOs.Materials;
using SpoolManager.Shared.DTOs.Suggestions;
using SpoolManager.Shared.DTOs.Tickets;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IMaterialRepository _materials;
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly ISpoolRepository _spools;
    private readonly ITicketRepository _tickets;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ISiteSettingsRepository _siteSettings;
    private readonly IOfdSyncService _ofdSync;
    private readonly ISuggestionRepository _suggestions;

    public AdminController(IMaterialRepository materials, IUserRepository users,
        IProjectRepository projects, ISpoolRepository spools,
        ITicketRepository tickets, IAuditService audit, IEmailService email,
        ISiteSettingsRepository siteSettings, IOfdSyncService ofdSync,
        ISuggestionRepository suggestions)
    {
        _materials = materials;
        _users = users;
        _projects = projects;
        _spools = spools;
        _tickets = tickets;
        _audit = audit;
        _email = email;
        _siteSettings = siteSettings;
        _ofdSync = ofdSync;
        _suggestions = suggestions;
    }

    private bool IsPlatformAdmin() =>
        User.FindFirst("is_platform_admin")?.Value == "true";

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("materials")]
    public async Task<IActionResult> GetGlobalMaterials([FromQuery] string? search)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var materials = await _materials.GetGlobalAsync(search);
        return Ok(materials.Select(MapMaterialToDto));
    }

    [HttpPost("materials")]
    public async Task<IActionResult> CreateGlobalMaterial(CreateMaterialRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var material = MaterialsController.MapFromRequest(request);
        material.ProjectId = null;
        var id = await _materials.CreateAsync(material);
        var created = await _materials.GetByIdAsync(id);
        await _audit.LogAsync("admin.material.create", userId: UserId, username: UserName,
            entityType: "material", entityId: id.ToString(), entityName: $"{request.Brand} {request.Type}",
            ipAddress: ClientIp);
        return CreatedAtAction(nameof(GetGlobalMaterials), new { }, MapMaterialToDto(created!));
    }

    [HttpPut("materials/{id}")]
    public async Task<IActionResult> UpdateGlobalMaterial(Guid id, UpdateMaterialRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var material = await _materials.GetByIdAsync(id);
        if (material == null || material.ProjectId != null)
            return NotFound();
        MaterialsController.ApplyRequest(material, request);
        material.UpdatedAt = DateTime.UtcNow;
        await _materials.UpdateAsync(material);
        await _audit.LogAsync("admin.material.update", userId: UserId, username: UserName,
            entityType: "material", entityId: id.ToString(), entityName: $"{material.Brand} {material.Type}",
            ipAddress: ClientIp);
        return Ok(MapMaterialToDto((await _materials.GetByIdAsync(id))!));
    }

    [HttpDelete("materials/{id}")]
    public async Task<IActionResult> DeleteGlobalMaterial(Guid id)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var material = await _materials.GetByIdAsync(id);
        if (material == null || material.ProjectId != null)
            return NotFound();
        await _audit.LogAsync("admin.material.delete", userId: UserId, username: UserName,
            entityType: "material", entityId: id.ToString(), entityName: $"{material.Brand} {material.Type}",
            ipAddress: ClientIp);
        await _materials.DeleteAsync(id);
        return NoContent();
    }

    private static FilamentMaterialDto MapMaterialToDto(FilamentMaterial m) => new()
    {
        Id = m.Id, Type = m.Type, ColorHex = m.ColorHex, Brand = m.Brand,
        MinTempCelsius = m.MinTempCelsius, MaxTempCelsius = m.MaxTempCelsius,
        ColorName = m.ColorName, DiameterMm = m.DiameterMm, WeightGrams = m.WeightGrams,
        BedTempCelsius = m.BedTempCelsius, DensityGCm3 = m.DensityGCm3,
        DryTempCelsius = m.DryTempCelsius, DryTimeHours = m.DryTimeHours,
        Notes = m.Notes, ReorderUrl = m.ReorderUrl, PricePerKg = m.PricePerKg,
        IsPublic = m.IsPublic, ReorderClickCount = m.ReorderClickCount,
        OfdFilamentId = m.OfdFilamentId, OfdVariantId = m.OfdVariantId,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var users = await _users.GetAllAsync();
        return Ok(users.Select(u => new AdminUserDto
        {
            Id = u.Id, Username = u.Username, Email = u.Email,
            IsPlatformAdmin = u.IsPlatformAdmin, IsSuperAdmin = u.IsSuperAdmin,
            EmailVerified = u.EmailVerified,
            CreatedAt = u.CreatedAt, LastActiveAt = u.LastActiveAt
        }));
    }

    [HttpPut("users/{id}/admin")]
    public async Task<IActionResult> SetAdminStatus(Guid id, [FromBody] SetAdminRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (id == UserId)
            return BadRequest("Cannot change own admin status.");
        var user = await _users.GetByIdAsync(id);
        if (user == null)
            return NotFound();
        if (user.IsSuperAdmin)
            return BadRequest("Cannot change the main admin's status.");

        await _users.SetAdminAsync(id, request.IsAdmin, user.TokenVersion + 1);
        await _audit.LogAsync(request.IsAdmin ? "admin.user.promote" : "admin.user.demote",
            userId: UserId, username: UserName,
            entityType: "user", entityId: id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);
        return NoContent();
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (id == UserId)
            return BadRequest("Cannot delete own account.");
        var user = await _users.GetByIdAsync(id);
        if (user == null)
            return NotFound();
        if (user.IsSuperAdmin)
            return BadRequest("Cannot delete the main admin.");

        await _audit.LogAsync("admin.user.delete",
            userId: UserId, username: UserName,
            entityType: "user", entityId: id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        await _users.DeleteAsync(id);
        return NoContent();
    }

    [HttpPut("users/{id}/onboarding/reset")]
    public async Task<IActionResult> ResetUserOnboarding(Guid id)
    {
        if (!IsPlatformAdmin())
            return Forbid();

        var user = await _users.GetByIdAsync(id);
        if (user == null)
            return NotFound();

        user.DashboardOnboardingDismissed = false;
        await _users.UpdateAsync(user);

        await _audit.LogAsync("admin.user.onboarding.reset",
            userId: UserId, username: UserName,
            entityType: "user", entityId: id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        return NoContent();
    }

    [HttpGet("settings/legal")]
    public async Task<IActionResult> GetLegalSettings()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        return Ok(new LegalSettingsDto
        {
            PrivacyDe = await _siteSettings.GetAsync("privacy_de") ?? string.Empty,
            PrivacyEn = await _siteSettings.GetAsync("privacy_en") ?? string.Empty,
            ImprintDe = await _siteSettings.GetAsync("imprint_de") ?? string.Empty,
            ImprintEn = await _siteSettings.GetAsync("imprint_en") ?? string.Empty,
            TermsDe = await _siteSettings.GetAsync("terms_de") ?? string.Empty,
            TermsEn = await _siteSettings.GetAsync("terms_en") ?? string.Empty,
        });
    }

    [HttpPut("settings/legal")]
    public async Task<IActionResult> SaveLegalSettings([FromBody] LegalSettingsDto dto)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        await _siteSettings.SetAsync("privacy_de", dto.PrivacyDe);
        await _siteSettings.SetAsync("privacy_en", dto.PrivacyEn);
        await _siteSettings.SetAsync("imprint_de", dto.ImprintDe);
        await _siteSettings.SetAsync("imprint_en", dto.ImprintEn);
        await _siteSettings.SetAsync("terms_de", dto.TermsDe);
        await _siteSettings.SetAsync("terms_en", dto.TermsEn);
        await _audit.LogAsync("admin.legal.update", userId: UserId, username: UserName, ipAddress: ClientIp);
        return NoContent();
    }

    [HttpGet("settings/smtp")]
    public async Task<IActionResult> GetSmtpSettings()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var s = await _email.GetSettingsAsync();
        if (s == null)
            return Ok(new SmtpSettingsDto());
        return Ok(new SmtpSettingsDto
        {
            Host = s.Host, Port = s.Port, UseSsl = s.UseSsl, UseStartTls = s.UseStartTls,
            Username = s.Username, Password = string.Empty,
            FromEmail = s.FromEmail, FromName = s.FromName,
            IsEnabled = s.IsEnabled, BaseUrl = s.BaseUrl
        });
    }

    [HttpPut("settings/smtp")]
    public async Task<IActionResult> SaveSmtpSettings([FromBody] SmtpSettingsDto dto)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var existing = await _email.GetSettingsAsync();
        var settings = new SpoolManager.Infrastructure.Data.SmtpSettings
        {
            Host = dto.Host.Trim(), Port = dto.Port,
            UseSsl = dto.UseSsl, UseStartTls = dto.UseStartTls,
            Username = dto.Username.Trim(),
            Password = string.IsNullOrWhiteSpace(dto.Password) && existing != null
                ? existing.Password : dto.Password,
            FromEmail = dto.FromEmail.Trim(), FromName = dto.FromName.Trim(),
            IsEnabled = dto.IsEnabled, BaseUrl = dto.BaseUrl.Trim()
        };
        await _email.SaveSettingsAsync(settings);
        await _audit.LogAsync("admin.smtp.update", userId: UserId, username: UserName, ipAddress: ClientIp);
        return NoContent();
    }

    [HttpPost("settings/smtp/test")]
    public async Task<IActionResult> TestSmtp([FromBody] TestSmtpRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            return BadRequest("Email required.");
        var ok = await _email.SendAsync(request.ToEmail, "Test", "SpoolManager SMTP-Test",
            "<p>Dies ist eine Test-E-Mail von SpoolManager. SMTP funktioniert! ✅</p>");
        return ok ? Ok(new { message = "Test email sent." }) : BadRequest(new { message = "Failed to send. Check SMTP settings." });
    }

    private static readonly string[] AllowedImageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml"];

    [HttpPost("settings/logo")]
    [RequestSizeLimit(4_194_304)]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (!AllowedImageTypes.Contains(file.ContentType))
            return BadRequest("Unsupported image type.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:{file.ContentType};base64,{base64}";
        await _siteSettings.SetAsync("branding_logo", dataUrl);
        await _audit.LogAsync("admin.branding.logo.update", userId: UserId, username: UserName, ipAddress: ClientIp);
        return Ok(new { dataUrl });
    }

    [HttpDelete("settings/logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        await _siteSettings.SetAsync("branding_logo", null);
        return NoContent();
    }

    [HttpPost("settings/favicon")]
    [RequestSizeLimit(524_288)]
    public async Task<IActionResult> UploadFavicon(IFormFile file)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (!AllowedImageTypes.Contains(file.ContentType))
            return BadRequest("Unsupported image type.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:{file.ContentType};base64,{base64}";
        await _siteSettings.SetAsync("branding_favicon", dataUrl);
        await _audit.LogAsync("admin.branding.favicon.update", userId: UserId, username: UserName, ipAddress: ClientIp);
        return Ok(new { dataUrl });
    }

    [HttpDelete("settings/favicon")]
    public async Task<IActionResult> DeleteFavicon()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        await _siteSettings.SetAsync("branding_favicon", null);
        return NoContent();
    }

    [HttpPost("settings/logo-dark")]
    [RequestSizeLimit(4_194_304)]
    public async Task<IActionResult> UploadLogoDark(IFormFile file)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (!AllowedImageTypes.Contains(file.ContentType))
            return BadRequest("Unsupported image type.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:{file.ContentType};base64,{base64}";
        await _siteSettings.SetAsync("branding_logo_dark", dataUrl);
        await _audit.LogAsync("admin.branding.logo.dark.update", userId: UserId, username: UserName, ipAddress: ClientIp);
        return Ok(new { dataUrl });
    }

    [HttpDelete("settings/logo-dark")]
    public async Task<IActionResult> DeleteLogoDark()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        await _siteSettings.SetAsync("branding_logo_dark", null);
        return NoContent();
    }

    [HttpPut("settings/landing-page")]
    public async Task<IActionResult> SetLandingPage([FromBody] SetLandingPageRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        await _siteSettings.SetAsync("landing_page_enabled", request.Enabled ? "true" : "false");
        return NoContent();
    }

    [HttpPost("ofd-sync")]
    public async Task<IActionResult> SyncOfd()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var result = await _ofdSync.SyncAsync();
        await _audit.LogAsync("admin.ofd.sync", userId: UserId, username: UserName,
            details: $"Created: {result.Created}, Updated: {result.Updated}, Skipped: {result.Skipped}",
            ipAddress: ClientIp);
        return Ok(result);
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string? status)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var suggestions = await _suggestions.GetAllAsync(status);
        var result = new List<MaterialSuggestionDto>();
        foreach (var s in suggestions)
        {
            string? materialName = null;
            if (s.MaterialId.HasValue)
            {
                var mat = await _materials.GetByIdAsync(s.MaterialId.Value);
                if (mat != null)
                materialName = $"{mat.Brand} {mat.Type}";
            }
            result.Add(MapSuggestionToDto(s, materialName));
        }
        return Ok(result);
    }

    [HttpGet("suggestions/count")]
    public async Task<IActionResult> GetSuggestionCount()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        return Ok(new { count = await _suggestions.CountPendingAsync() });
    }

    [HttpPost("suggestions/{id}/review")]
    public async Task<IActionResult> ReviewSuggestion(Guid id, [FromBody] ReviewSuggestionRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var suggestion = await _suggestions.GetByIdAsync(id);
        if (suggestion == null)
            return NotFound();

        suggestion.Status = request.Status;
        suggestion.AdminNotes = request.AdminNotes;
        suggestion.ReviewedAt = DateTime.UtcNow;
        suggestion.ReviewedByUserId = UserId;
        await _suggestions.UpdateAsync(suggestion);

        if (request.Status == MaterialSuggestion.StatusApproved)
        {
            if (suggestion.MaterialId.HasValue)
            {
                var existing = await _materials.GetByIdAsync(suggestion.MaterialId.Value);
                if (existing != null)
                {
                    existing.Type = suggestion.Type;
                    existing.Brand = suggestion.Brand;
                    existing.ColorHex = suggestion.ColorHex;
                    existing.ColorName = suggestion.ColorName;
                    existing.MinTempCelsius = suggestion.MinTempCelsius;
                    existing.MaxTempCelsius = suggestion.MaxTempCelsius;
                    existing.BedTempCelsius = suggestion.BedTempCelsius;
                    existing.DiameterMm = suggestion.DiameterMm;
                    existing.DensityGCm3 = suggestion.DensityGCm3;
                    existing.DryTempCelsius = suggestion.DryTempCelsius;
                    existing.DryTimeHours = suggestion.DryTimeHours;
                    existing.Notes = suggestion.Notes;
                    existing.ReorderUrl = suggestion.ReorderUrl;
                    existing.PricePerKg = suggestion.PricePerKg;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _materials.UpdateAsync(existing);
                }
            }
            else
            {
                var newMaterial = new FilamentMaterial
                {
                    Type = suggestion.Type,
                    Brand = suggestion.Brand,
                    ColorHex = suggestion.ColorHex,
                    ColorName = suggestion.ColorName,
                    MinTempCelsius = suggestion.MinTempCelsius,
                    MaxTempCelsius = suggestion.MaxTempCelsius,
                    BedTempCelsius = suggestion.BedTempCelsius,
                    DiameterMm = suggestion.DiameterMm,
                    DensityGCm3 = suggestion.DensityGCm3,
                    DryTempCelsius = suggestion.DryTempCelsius,
                    DryTimeHours = suggestion.DryTimeHours,
                    Notes = suggestion.Notes,
                    ReorderUrl = suggestion.ReorderUrl,
                    PricePerKg = suggestion.PricePerKg,
                    ProjectId = null,
                    IsPublic = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _materials.CreateAsync(newMaterial);
            }
        }

        await _audit.LogAsync($"admin.suggestion.{request.Status}", userId: UserId, username: UserName,
            entityType: "suggestion", entityId: id.ToString(),
            entityName: $"{suggestion.Brand} {suggestion.Type}",
            ipAddress: ClientIp);

        return NoContent();
    }

    private static MaterialSuggestionDto MapSuggestionToDto(MaterialSuggestion s, string? materialName) => new()
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

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var now = DateTime.UtcNow;
        var allUsers = await _users.GetAllAsync();
        var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoff15Min = now.AddMinutes(-15);
        var globalMaterials = await _materials.GetGlobalAsync();
        var topClicked = await _materials.GetTopClickedGlobalAsync(5);
        var stats = new AdminStatsDto
        {
            TotalUsers = allUsers.Count,
            ActiveUsersLast15Min = allUsers.Count(u => u.LastActiveAt.HasValue && u.LastActiveAt.Value >= cutoff15Min),
            NewUsersThisMonth = allUsers.Count(u => u.CreatedAt >= firstOfMonth),
            TotalProjects = await _projects.GetTotalCountAsync(),
            ProjectsWithSpools = await _projects.GetWithSpoolsCountAsync(),
            TotalSpools = await _spools.GetTotalCountAsync(),
            TotalGlobalMaterials = globalMaterials.Count,
            TotalReorderClicks = globalMaterials.Sum(m => m.ReorderClickCount),
            OpenTickets = await _tickets.CountByStatusAsync(TicketStatus.Open),
            InProgressTickets = await _tickets.CountByStatusAsync(TicketStatus.InProgress),
            TopClickedMaterials = topClicked.Select(m => new MaterialClickDto
            {
                Id = m.Id, Brand = m.Brand, Type = m.Type,
                ColorHex = m.ColorHex, ClickCount = m.ReorderClickCount
            }).ToList()
        };
        return Ok(stats);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetAllTickets([FromQuery] string? status, [FromQuery] string? search)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        TicketStatus? statusFilter = status switch
        {
            "open" => TicketStatus.Open,
            "in_progress" => TicketStatus.InProgress,
            "closed" => TicketStatus.Closed,
            "answered" => TicketStatus.Answered,
            _ => null
        };
        var tickets = await _tickets.GetAllAsync(statusFilter, search);
        var result = new List<SupportTicketDto>();
        foreach (var t in tickets)
        {
            var comments = await _tickets.GetCommentsAsync(t.Id);
            result.Add(MapTicketToDto(t, comments.Count(c => !c.IsInternal)));
        }
        return Ok(result);
    }

    [HttpPut("tickets/{id}/status")]
    public async Task<IActionResult> SetTicketStatus(Guid id, [FromBody] UpdateTicketStatusRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null)
            return NotFound();
        ticket.Status = request.Status switch
        {
            "in_progress" => TicketStatus.InProgress,
            "closed" => TicketStatus.Closed,
            "answered" => TicketStatus.Answered,
            _ => TicketStatus.Open
        };
        await _tickets.UpdateAsync(ticket);
        return NoContent();
    }

    [HttpPut("tickets/{id}/assign")]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] AssignTicketRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null)
            return NotFound();
        if (request.AssignedToUserId.HasValue)
        {
            var assignee = await _users.GetByIdAsync(request.AssignedToUserId.Value);
            if (assignee == null || !assignee.IsPlatformAdmin)
                return BadRequest("User is not an admin.");
            ticket.AssignedToUserId = assignee.Id;
            ticket.AssignedToUsername = assignee.Username;
        }
        else
        {
            ticket.AssignedToUserId = null;
            ticket.AssignedToUsername = null;
        }
        await _tickets.UpdateAsync(ticket);
        return NoContent();
    }

    [HttpPost("tickets/{id}/comments")]
    public async Task<IActionResult> AddAdminComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        if (!IsPlatformAdmin())
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content required.");
        var ticket = await _tickets.GetByIdAsync(id);
        if (ticket == null)
            return NotFound();

        var comment = new TicketComment
        {
            TicketId = id, UserId = UserId,
            Username = UserName ?? "Admin", IsAdmin = true,
            IsInternal = request.IsInternal,
            Content = request.Content.Trim()
        };
        await _tickets.AddCommentAsync(comment);

        if (!request.IsInternal && ticket.Status != TicketStatus.Closed)
            ticket.Status = TicketStatus.Answered;

        await _tickets.UpdateAsync(ticket);

        if (!request.IsInternal)
        {
            var owner = await _users.GetByIdAsync(ticket.UserId);
            if (owner != null && owner.Id != UserId)
                await _email.NotifyTicketReplyAsync(ticket, owner, request.Content.Trim(), replyIsFromAdmin: true);
        }

        return NoContent();
    }

    internal static SupportTicketDto MapTicketToDto(SupportTicket t, int commentCount) => new()
    {
        Id = t.Id, UserId = t.UserId, Username = t.Username,
        Subject = t.Subject, Description = t.Description,
        Status = t.Status switch
        {
            TicketStatus.InProgress => "in_progress",
            TicketStatus.Closed => "closed",
            TicketStatus.Answered => "answered",
            _ => "open"
        },
        AssignedToUserId = t.AssignedToUserId,
        AssignedToUsername = t.AssignedToUsername,
        CommentCount = commentCount,
        CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt
    };
}

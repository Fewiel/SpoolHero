using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.DTOs.Projects;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IInvitationRepository _invitations;
    private readonly IAuditService _audit;

    public ProjectsController(IProjectRepository projects, IProjectMemberRepository members, IInvitationRepository invitations, IAuditService audit)
    {
        _projects = projects;
        _members = members;
        _invitations = invitations;
        _audit = audit;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetMyProjects()
    {
        var projects = await _projects.GetForUserAsync(UserId);
        var memberEntries = new List<ProjectMember>();
        foreach (var p in projects)
            memberEntries.Add((await _members.GetAsync(p.Id, UserId))!);

        var dtos = projects.Select((p, i) => new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            MyRole = memberEntries.FirstOrDefault(m => m.ProjectId == p.Id)?.Role ?? "member",
            CreatedAt = p.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });
        if (request.Name.Length > 200)
            return BadRequest(new { message = "Name must not exceed 200 characters." });

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description
        };
        var id = await _projects.CreateAsync(project);

        var member = new ProjectMember
        {
            ProjectId = id,
            UserId = UserId,
            Role = "admin"
        };
        await _members.AddAsync(member);

        await _audit.LogAsync("project.create",
            userId: UserId, username: UserName,
            entityType: "project", entityId: id.ToString(), entityName: request.Name,
            projectId: id, projectName: request.Name,
            ipAddress: ClientIp);

        var created = await _projects.GetByIdAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, new ProjectDto
        {
            Id = created!.Id,
            Name = created.Name,
            Description = created.Description,
            MyRole = "admin",
            CreatedAt = created.CreatedAt
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await _members.IsMemberAsync(id, UserId)) return Forbid();
        var project = await _projects.GetByIdAsync(id);
        if (project == null) return NotFound();
        var member = await _members.GetAsync(id, UserId);
        return Ok(new ProjectDto { Id = project.Id, Name = project.Name, Description = project.Description, MyRole = member?.Role ?? "member", CreatedAt = project.CreatedAt });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest request)
    {
        var member = await _members.GetAsync(id, UserId);
        if (member == null) return Forbid();
        if (member.Role != "admin") return StatusCode(403, new { message = "Only admins can update the project." });

        var project = await _projects.GetByIdAsync(id);
        if (project == null) return NotFound();
        project.Name = request.Name;
        project.Description = request.Description;
        await _projects.UpdateAsync(project);
        return Ok(new ProjectDto { Id = project.Id, Name = project.Name, Description = project.Description, MyRole = "admin", CreatedAt = project.CreatedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var member = await _members.GetAsync(id, UserId);
        if (member == null) return Forbid();
        if (member.Role != "admin") return StatusCode(403, new { message = "Only admins can delete the project." });

        var project = await _projects.GetByIdAsync(id);
        if (project == null) return NotFound();

        await _audit.LogAsync("project.delete",
            userId: UserId, username: UserName,
            entityType: "project", entityId: id.ToString(), entityName: project.Name,
            projectId: id, projectName: project.Name,
            ipAddress: ClientIp);

        await _projects.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        if (!await _members.IsMemberAsync(id, UserId)) return Forbid();
        var members = await _members.GetByProjectAsync(id);
        var dtos = members.Select(m => new ProjectMemberDto
        {
            Id = m.Id,
            UserId = m.UserId,
            Username = m.Username ?? string.Empty,
            Email = m.Email ?? string.Empty,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        }).ToList();
        return Ok(dtos);
    }

    [HttpPut("{id}/members/{userId}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, UpdateMemberRoleRequest request)
    {
        var myMember = await _members.GetAsync(id, UserId);
        if (myMember == null) return Forbid();
        if (myMember.Role != "admin") return StatusCode(403, new { message = "Only admins can change roles." });
        if (!new[] { "admin", "manager", "member" }.Contains(request.Role))
            return BadRequest(new { message = "Invalid role." });

        var target = await _members.GetAsync(id, userId);
        if (target == null) return NotFound();

        if (target.Role == "admin" && request.Role != "admin")
        {
            var admins = (await _members.GetByProjectAsync(id)).Count(m => m.Role == "admin");
            if (admins <= 1)
                return BadRequest(new { message = "Cannot demote the last admin." });
        }

        var oldRole = target.Role;
        target.Role = request.Role;
        await _members.UpdateAsync(target);

        var project = await _projects.GetByIdAsync(id);
        await _audit.LogAsync("project.member.role",
            userId: UserId, username: UserName,
            entityType: "user", entityId: userId.ToString(), entityName: target.Username,
            projectId: id, projectName: project?.Name,
            details: $"{oldRole} → {request.Role}",
            ipAddress: ClientIp);

        return Ok();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var myMember = await _members.GetAsync(id, UserId);
        if (myMember == null) return Forbid();
        if (myMember.Role != "admin" && UserId != userId) return StatusCode(403, new { message = "Only admins can remove members." });

        var target = await _members.GetAsync(id, userId);
        if (target == null) return NotFound();

        if (target.Role == "admin")
        {
            var admins = (await _members.GetByProjectAsync(id)).Count(m => m.Role == "admin");
            if (admins <= 1)
                return BadRequest(new { message = "Cannot remove the last admin." });
        }

        var project = await _projects.GetByIdAsync(id);

        await _audit.LogAsync("project.member.remove",
            userId: UserId, username: UserName,
            entityType: "user", entityId: userId.ToString(), entityName: target?.Username,
            projectId: id, projectName: project?.Name,
            ipAddress: ClientIp);

        await _members.RemoveAsync(id, userId);
        return NoContent();
    }

    [HttpPost("{id}/invitations")]
    public async Task<IActionResult> CreateInvitation(Guid id, CreateInvitationRequest request)
    {
        var myMember = await _members.GetAsync(id, UserId);
        if (myMember == null) return Forbid();
        if (myMember.Role == "member") return StatusCode(403, new { message = "Only admins and managers can invite." });
        if (!new[] { "admin", "manager", "member" }.Contains(request.Role))
            return BadRequest(new { message = "Invalid role." });
        if (request.Role == "admin" && myMember.Role != "admin")
            return StatusCode(403, new { message = "Only admins can invite as admin." });

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var invitation = new Invitation
        {
            ProjectId = id,
            InvitedByUserId = UserId,
            Token = token,
            Role = request.Role,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        var invId = await _invitations.CreateAsync(invitation);

        var project = await _projects.GetByIdAsync(id);
        await _audit.LogAsync("project.invitation.create",
            userId: UserId, username: UserName,
            entityType: "project", entityId: id.ToString(), entityName: project?.Name,
            projectId: id, projectName: project?.Name,
            details: $"Role: {request.Role}",
            ipAddress: ClientIp);

        return Ok(new InvitationDto
        {
            Id = invId,
            Token = token,
            Role = request.Role,
            ExpiresAt = invitation.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        });
    }

    [HttpGet("{id}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid id)
    {
        var myMember = await _members.GetAsync(id, UserId);
        if (myMember == null) return Forbid();
        if (myMember.Role == "member") return StatusCode(403, new { message = "Only admins and managers can view invitations." });
        var invitations = await _invitations.GetByProjectAsync(id);
        var dtos = invitations.Select(i => new InvitationDto
        {
            Id = i.Id,
            Token = i.Token,
            Role = i.Role,
            ExpiresAt = i.ExpiresAt,
            CreatedAt = i.CreatedAt,
            IsUsed = i.UsedAt.HasValue
        }).ToList();
        return Ok(dtos);
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinProject(AcceptInvitationRequest request)
    {
        var invitation = await _invitations.GetByTokenAsync(request.Token);
        if (invitation == null || invitation.UsedAt.HasValue || invitation.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired invitation." });

        if (await _members.IsMemberAsync(invitation.ProjectId, UserId))
            return Conflict(new { message = "Already a member of this project." });

        var member = new ProjectMember
        {
            ProjectId = invitation.ProjectId,
            UserId = UserId,
            Role = invitation.Role
        };
        await _members.AddAsync(member);
        await _invitations.MarkUsedAsync(invitation.Id, UserId);

        var project = await _projects.GetByIdAsync(invitation.ProjectId);
        await _audit.LogAsync("project.member.join",
            userId: UserId, username: UserName,
            entityType: "project", entityId: invitation.ProjectId.ToString(), entityName: project?.Name,
            projectId: invitation.ProjectId, projectName: project?.Name,
            details: $"Role: {invitation.Role}",
            ipAddress: ClientIp);

        return Ok(new ProjectDto { Id = project!.Id, Name = project.Name, Description = project.Description, MyRole = invitation.Role, CreatedAt = project.CreatedAt });
    }

    [HttpGet("invitations/info/{token}")]
    public async Task<IActionResult> GetInvitationInfo(string token)
    {
        var invitation = await _invitations.GetByTokenAsync(token);
        if (invitation == null)
            return Ok(new InvitationInfoDto { IsValid = false, ErrorMessage = "Invitation not found." });
        if (invitation.UsedAt.HasValue)
            return Ok(new InvitationInfoDto { IsValid = false, ErrorMessage = "Invitation already used." });
        if (invitation.ExpiresAt < DateTime.UtcNow)
            return Ok(new InvitationInfoDto { IsValid = false, ErrorMessage = "Invitation expired." });

        return Ok(new InvitationInfoDto
        {
            IsValid = true,
            ProjectName = invitation.ProjectName ?? string.Empty,
            InvitedByUsername = invitation.InvitedByUsername ?? string.Empty,
            Role = invitation.Role
        });
    }
}

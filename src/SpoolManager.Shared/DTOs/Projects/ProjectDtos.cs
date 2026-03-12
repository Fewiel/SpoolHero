namespace SpoolManager.Shared.DTOs.Projects;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MyRole { get; set; } = "member";
    public DateTime CreatedAt { get; set; }
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ProjectMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; }
}

public class CreateInvitationRequest
{
    public string Role { get; set; } = "member";
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsUsed { get; set; }
}

public class AcceptInvitationRequest
{
    public string Token { get; set; } = string.Empty;
}

public class InvitationInfoDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string InvitedByUsername { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateMemberRoleRequest
{
    public string Role { get; set; } = "member";
}

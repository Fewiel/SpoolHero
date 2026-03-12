namespace SpoolManager.Shared.Models;

public class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}

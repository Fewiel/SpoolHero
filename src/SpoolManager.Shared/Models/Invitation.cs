namespace SpoolManager.Shared.Models;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public Guid? UsedByUserId { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ProjectName { get; set; }
    public string? InvitedByUsername { get; set; }
}

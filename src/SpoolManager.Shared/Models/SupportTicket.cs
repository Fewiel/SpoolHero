namespace SpoolManager.Shared.Models;

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Closed = 2,
    Answered = 3
}

public class SupportTicket
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

namespace SpoolManager.Shared.DTOs.Tickets;

public class SupportTicketDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public int CommentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TicketDetailDto : SupportTicketDto
{
    public List<TicketCommentDto> Comments { get; set; } = [];
}

public class TicketCommentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsInternal { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CreateCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class UpdateTicketStatusRequest
{
    public string Status { get; set; } = "open";
}

public class AssignTicketRequest
{
    public Guid? AssignedToUserId { get; set; }
}

namespace SpoolManager.Infrastructure.Data;

public class AppUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool EmailVerified { get; set; } = true;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpires { get; set; }
    public bool NotifySpoolLow { get; set; } = true;
    public bool NotifyDryerDone { get; set; } = true;
    public bool NotifyTicketReply { get; set; } = true;
    public string PreferredLanguage { get; set; } = "en";
    public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

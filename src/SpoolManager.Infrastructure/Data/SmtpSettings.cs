namespace SpoolManager.Infrastructure.Data;

public class SmtpSettings
{
    public Guid Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "SpoolManager";
    public bool IsEnabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
}

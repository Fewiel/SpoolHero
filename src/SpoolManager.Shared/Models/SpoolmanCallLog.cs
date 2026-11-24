namespace SpoolManager.Shared.Models;

public class SpoolmanCallLog
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public DateTime CalledAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}

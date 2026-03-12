namespace SpoolManager.Shared.Models;

public class SiteSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}

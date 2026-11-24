namespace SpoolManager.Shared.DTOs.Webhooks;

public class WebhookDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? ChatId { get; set; }
    public bool HasBotToken { get; set; }
    public string? BotTokenMasked { get; set; }
    public bool IsEnabled { get; set; }
    public bool OnDryerFinished { get; set; }
    public bool OnSpoolLow { get; set; }
    public bool OnSpoolConsumed { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public bool? LastSuccess { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveWebhookRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "generic";
    public string? Url { get; set; }
    // Telegram bot token. Empty (or the masked value) on update keeps the stored token.
    public string? BotToken { get; set; }
    public string? ChatId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool OnDryerFinished { get; set; } = true;
    public bool OnSpoolLow { get; set; } = true;
    public bool OnSpoolConsumed { get; set; } = true;
}

public class WebhookTestResult
{
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
}

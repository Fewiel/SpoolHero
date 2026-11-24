namespace SpoolManager.Shared.Models;

public class Webhook
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = WebhookTypes.Generic;
    public string? Url { get; set; }
    public string? BotToken { get; set; }
    public string? ChatId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool OnDryerFinished { get; set; } = true;
    public bool OnSpoolLow { get; set; } = true;
    public bool OnSpoolConsumed { get; set; } = true;
    public DateTime? LastTriggeredAt { get; set; }
    public bool? LastSuccess { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class WebhookTypes
{
    public const string Discord = "discord";
    public const string Telegram = "telegram";
    public const string HomeAssistant = "homeassistant";
    public const string Generic = "generic";

    public static readonly string[] All = [Discord, Telegram, HomeAssistant, Generic];
}

public static class WebhookEvents
{
    public const string DryerFinished = "dryer.finished";
    public const string SpoolLow = "spool.low";
    public const string SpoolConsumed = "spool.consumed";
    public const string Test = "test";
}

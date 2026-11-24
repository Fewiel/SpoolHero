using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services.Webhooks;

/// <summary>
/// Snapshot of an event that is delivered to the webhooks of a project.
/// Captured at the time the event happens so delivery does not depend on later DB state.
/// </summary>
public class WebhookEventData
{
    public string EventType { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public WebhookSpoolData? Spool { get; init; }
    public WebhookMaterialData? Material { get; init; }
    public WebhookDryerData? Dryer { get; init; }

    public static WebhookEventData ForSpool(string eventType, Spool spool) => new()
    {
        EventType = eventType,
        ProjectId = spool.ProjectId,
        Spool = new WebhookSpoolData
        {
            Id = spool.Id,
            RemainingPercent = spool.RemainingPercent,
            RemainingWeightGrams = spool.RemainingWeightGrams,
            RfidTagUid = spool.RfidTagUid,
            OwnerName = spool.OwnerName,
            ConsumedAt = spool.ConsumedAt
        },
        Material = spool.FilamentMaterial == null ? null : WebhookMaterialData.From(spool.FilamentMaterial)
    };

    public static WebhookEventData ForDryer(Dryer dryer) => new()
    {
        EventType = WebhookEvents.DryerFinished,
        ProjectId = dryer.ProjectId,
        Dryer = new WebhookDryerData
        {
            Id = dryer.Id,
            Name = dryer.Name,
            DryingStartedAt = dryer.DryingStartedAt,
            DryingFinishAt = dryer.DryingFinishAt
        }
    };

    public static WebhookEventData ForTest(Guid projectId) => new()
    {
        EventType = WebhookEvents.Test,
        ProjectId = projectId
    };
}

public class WebhookSpoolData
{
    public Guid Id { get; init; }
    public decimal RemainingPercent { get; init; }
    public decimal RemainingWeightGrams { get; init; }
    public string? RfidTagUid { get; init; }
    public string? OwnerName { get; init; }
    public DateTime? ConsumedAt { get; init; }
}

public class WebhookMaterialData
{
    public Guid Id { get; init; }
    public string Brand { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? ColorName { get; init; }
    public string ColorHex { get; init; } = "FFFFFF";
    public decimal DiameterMm { get; init; }

    public static WebhookMaterialData From(FilamentMaterial m) => new()
    {
        Id = m.Id,
        Brand = m.Brand,
        Type = m.Type,
        ColorName = m.ColorName,
        ColorHex = m.ColorHex,
        DiameterMm = m.DiameterMm
    };
}

public class WebhookDryerData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime? DryingStartedAt { get; init; }
    public DateTime? DryingFinishAt { get; init; }
}

namespace BuildingBlocks.Messaging.Models;

public sealed record IntegrationEvent(
    Guid EventId,
    string EventType,
    Guid? TenantId,
    string? CorrelationId,
    DateTimeOffset OccurredAt,
    object Payload);

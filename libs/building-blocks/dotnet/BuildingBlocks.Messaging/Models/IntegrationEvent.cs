namespace BuildingBlocks.Messaging.Models;

public sealed record IntegrationEvent(
    Guid EventId,
    string EventType,
    Guid? TenantId,
    string? CorrelationId,
    DateTimeOffset OccurredAt,
    object Payload,
    string? RoutingKey = null,
    string Exchange = "signaldesk.domain-events",
    int EventVersion = 1,
    string? AggregateType = null,
    string? AggregateId = null,
    long? AggregateVersion = null,
    Guid? CausationId = null,
    Guid? ActorId = null,
    IReadOnlyDictionary<string, object?>? Headers = null);

namespace BuildingBlocks.Messaging.Outbox;

public sealed record OutboxEventEnvelope(
    string ServiceName,
    string EventType,
    int EventVersion,
    string RoutingKey,
    string Payload,
    string Headers,
    Guid? TenantId = null,
    string Exchange = "signaldesk.domain-events",
    string? AggregateType = null,
    string? AggregateId = null,
    long? AggregateVersion = null,
    string? CorrelationId = null,
    Guid? CausationId = null,
    Guid? ActorId = null,
    int MaxRetries = 10);

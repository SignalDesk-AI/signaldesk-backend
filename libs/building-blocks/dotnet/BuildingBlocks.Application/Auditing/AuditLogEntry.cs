namespace BuildingBlocks.Application.Auditing;

public sealed record AuditLogEntry(
    Guid? TenantId,
    Guid? ActorUserId,
    string Action,
    string? AggregateType,
    string? AggregateId,
    string? CorrelationId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Metadata);

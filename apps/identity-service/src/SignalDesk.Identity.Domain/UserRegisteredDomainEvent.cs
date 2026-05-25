using BuildingBlocks.Domain.Events;

namespace SignalDesk.Identity.Domain;

public sealed record UserRegisteredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string? CorrelationId) : IDomainEvent
{
    public string EventType => IdentityDomainEventTypes.UserRegistered;

    public Guid? TenantId => null;
}

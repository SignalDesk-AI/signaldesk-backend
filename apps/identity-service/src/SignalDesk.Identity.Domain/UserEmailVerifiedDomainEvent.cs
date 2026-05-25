using BuildingBlocks.Domain.Events;

namespace SignalDesk.Identity.Domain;

public sealed record UserEmailVerifiedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    DateTimeOffset VerifiedAt,
    string? CorrelationId) : IDomainEvent
{
    public string EventType => IdentityDomainEventTypes.UserEmailVerified;

    public Guid? TenantId => null;
}

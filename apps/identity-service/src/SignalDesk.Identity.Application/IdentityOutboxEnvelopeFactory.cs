using System.Text.Json;
using BuildingBlocks.Messaging.Outbox;
using SignalDesk.Identity.Domain;

namespace SignalDesk.Identity.Application;

public static class IdentityOutboxEnvelopeFactory
{
    public const string ServiceName = "identity-service";
    public const string DomainEventsExchange = "signaldesk.domain-events";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static OutboxEventEnvelope UserRegistered(UserRegisteredDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var payload = new IdentityEventEnvelope<UserRegisteredPayload>(
            domainEvent.EventId,
            domainEvent.EventType,
            domainEvent.TenantId,
            domainEvent.CorrelationId,
            domainEvent.OccurredAt,
            new UserRegisteredPayload(domainEvent.UserId, domainEvent.Email));

        return Create(
            domainEvent.EventType,
            payload,
            domainEvent.TenantId,
            nameof(UserAccount),
            domainEvent.UserId.ToString(),
            domainEvent.CorrelationId);
    }

    public static OutboxEventEnvelope UserEmailVerified(UserEmailVerifiedDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var payload = new IdentityEventEnvelope<UserEmailVerifiedPayload>(
            domainEvent.EventId,
            domainEvent.EventType,
            domainEvent.TenantId,
            domainEvent.CorrelationId,
            domainEvent.OccurredAt,
            new UserEmailVerifiedPayload(domainEvent.UserId, domainEvent.Email, domainEvent.VerifiedAt));

        return Create(
            domainEvent.EventType,
            payload,
            domainEvent.TenantId,
            nameof(UserAccount),
            domainEvent.UserId.ToString(),
            domainEvent.CorrelationId);
    }

    private static OutboxEventEnvelope Create<TPayload>(
        string eventType,
        IdentityEventEnvelope<TPayload> payload,
        Guid? tenantId,
        string aggregateType,
        string aggregateId,
        string? correlationId)
    {
        return new OutboxEventEnvelope(
            ServiceName,
            eventType,
            EventVersion: 1,
            RoutingKey: eventType,
            Payload: JsonSerializer.Serialize(payload, JsonOptions),
            Headers: "{}",
            TenantId: tenantId,
            Exchange: DomainEventsExchange,
            AggregateType: aggregateType,
            AggregateId: aggregateId,
            CorrelationId: correlationId);
    }

    private sealed record IdentityEventEnvelope<TPayload>(
        Guid EventId,
        string EventType,
        Guid? TenantId,
        string? CorrelationId,
        DateTimeOffset OccurredAt,
        TPayload Payload);

    private sealed record UserRegisteredPayload(Guid UserId, string Email);

    private sealed record UserEmailVerifiedPayload(Guid UserId, string Email, DateTimeOffset VerifiedAt);
}

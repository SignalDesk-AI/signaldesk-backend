namespace BuildingBlocks.Messaging.Outbox;

public interface IOutboxStore
{
    Task AddAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OutboxEvent>> ClaimPendingAsync(
        string serviceName,
        string claimedBy,
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(Guid eventId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid eventId,
        string error,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default);

    Task ReclaimStaleAsync(
        string serviceName,
        DateTimeOffset claimedBefore,
        CancellationToken cancellationToken = default);
}

namespace BuildingBlocks.Application.Redis;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(
        Guid tenantId,
        string scope,
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> TryBeginAsync(
        Guid tenantId,
        string scope,
        string key,
        string requestHash,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid tenantId,
        string scope,
        string key,
        int responseCode,
        string? responseBody,
        CancellationToken cancellationToken = default);
}

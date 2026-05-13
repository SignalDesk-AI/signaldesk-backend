using BuildingBlocks.Application.Redis;

namespace BuildingBlocks.Infrastructure.Redis;

public sealed class NoopIdempotencyStore : IIdempotencyStore
{
    public Task<IdempotencyRecord?> GetAsync(
        Guid tenantId,
        string scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IdempotencyRecord?>(null);
    }

    public Task<bool> TryBeginAsync(
        Guid tenantId,
        string scope,
        string key,
        string requestHash,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task CompleteAsync(
        Guid tenantId,
        string scope,
        string key,
        int responseCode,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

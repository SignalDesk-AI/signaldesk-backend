using BuildingBlocks.Application.Redis;

namespace BuildingBlocks.Infrastructure.Redis;

public sealed class NoopDistributedLockProvider : IDistributedLockProvider
{
    public Task<IDistributedLock> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDistributedLock>(new NoopDistributedLock(key, string.Empty, false));
    }
}

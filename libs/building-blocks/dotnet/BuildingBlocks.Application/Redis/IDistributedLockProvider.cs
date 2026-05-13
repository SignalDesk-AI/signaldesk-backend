namespace BuildingBlocks.Application.Redis;

public interface IDistributedLockProvider
{
    Task<IDistributedLock> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}

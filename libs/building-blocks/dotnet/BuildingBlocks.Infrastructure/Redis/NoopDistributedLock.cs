using BuildingBlocks.Application.Redis;

namespace BuildingBlocks.Infrastructure.Redis;

public sealed class NoopDistributedLock : IDistributedLock
{
    public NoopDistributedLock(string key, string token, bool acquired)
    {
        Key = key;
        Token = token;
        Acquired = acquired;
    }

    public string Key { get; }

    public string Token { get; }

    public bool Acquired { get; }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

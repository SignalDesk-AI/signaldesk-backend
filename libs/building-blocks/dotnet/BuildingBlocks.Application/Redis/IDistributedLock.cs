namespace BuildingBlocks.Application.Redis;

public interface IDistributedLock : IAsyncDisposable
{
    string Key { get; }

    string Token { get; }

    bool Acquired { get; }
}

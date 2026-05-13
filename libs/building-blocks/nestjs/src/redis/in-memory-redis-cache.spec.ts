import { InMemoryRedisCache } from './in-memory-redis-cache';

describe('InMemoryRedisCache', () => {
  it('stores and deletes string values', async () => {
    const cache = new InMemoryRedisCache();

    await cache.set('key-1', 'value-1');
    await expect(cache.get('key-1')).resolves.toBe('value-1');

    await cache.delete('key-1');
    await expect(cache.get('key-1')).resolves.toBeUndefined();
  });

  it('expires values after ttl', async () => {
    jest.useFakeTimers();
    const cache = new InMemoryRedisCache();

    await cache.set('key-1', 'value-1', { ttlMs: 1000 });
    jest.advanceTimersByTime(1001);

    await expect(cache.get('key-1')).resolves.toBeUndefined();
    jest.useRealTimers();
  });
});

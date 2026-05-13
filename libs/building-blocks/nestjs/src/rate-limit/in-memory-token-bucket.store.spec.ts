import { InMemoryTokenBucketStore } from './in-memory-token-bucket.store';

describe('InMemoryTokenBucketStore', () => {
  it('allows requests while tokens remain', async () => {
    const store = new InMemoryTokenBucketStore();
    const options = { capacity: 2, refillTokens: 1, refillIntervalMs: 1000 };

    await expect(store.consume({ key: 'tenant-1' }, options, 0)).resolves.toMatchObject({
      allowed: true,
      remainingTokens: 1,
      retryAfterMs: 0,
    });
    await expect(store.consume({ key: 'tenant-1' }, options, 0)).resolves.toMatchObject({
      allowed: true,
      remainingTokens: 0,
      retryAfterMs: 0,
    });
  });

  it('rate limits when the bucket is empty and reports retry timing', async () => {
    const store = new InMemoryTokenBucketStore();
    const options = { capacity: 1, refillTokens: 1, refillIntervalMs: 1000 };

    await store.consume({ key: 'tenant-1' }, options, 0);
    const result = await store.consume({ key: 'tenant-1' }, options, 0);

    expect(result).toMatchObject({
      allowed: false,
      remainingTokens: 0,
      retryAfterMs: 1000,
    });
    expect(result.resetAt).toBe(new Date(1000).toISOString());
  });

  it('refills tokens over time', async () => {
    const store = new InMemoryTokenBucketStore();
    const options = { capacity: 1, refillTokens: 1, refillIntervalMs: 1000 };

    await store.consume({ key: 'tenant-1' }, options, 0);
    await expect(store.consume({ key: 'tenant-1' }, options, 1000)).resolves.toMatchObject({
      allowed: true,
      remainingTokens: 0,
    });
  });
});

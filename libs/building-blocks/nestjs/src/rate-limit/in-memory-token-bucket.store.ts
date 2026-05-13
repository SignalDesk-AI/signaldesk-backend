import {
  TokenBucketKey,
  TokenBucketOptions,
  TokenBucketState,
  TokenBucketStore,
  validateTokenBucketOptions,
} from './token-bucket';

interface BucketRecord {
  tokens: number;
  updatedAtMs: number;
}

export class InMemoryTokenBucketStore implements TokenBucketStore {
  private readonly buckets = new Map<string, BucketRecord>();

  async consume(
    key: TokenBucketKey,
    options: TokenBucketOptions,
    nowMs = Date.now()
  ): Promise<TokenBucketState> {
    validateTokenBucketOptions(options);

    const current = this.refill(this.buckets.get(key.key), options, nowMs);
    const allowed = current.tokens >= 1;

    if (allowed) {
      current.tokens -= 1;
    }

    this.buckets.set(key.key, current);

    const nextTokenMs = allowed
      ? 0
      : Math.ceil(((1 - current.tokens) / options.refillTokens) * options.refillIntervalMs);

    return {
      allowed,
      remainingTokens: Math.floor(current.tokens),
      retryAfterMs: nextTokenMs,
      resetAt: new Date(nowMs + nextTokenMs).toISOString(),
    };
  }

  private refill(
    record: BucketRecord | undefined,
    options: TokenBucketOptions,
    nowMs: number
  ): BucketRecord {
    if (!record) {
      return { tokens: options.capacity, updatedAtMs: nowMs };
    }

    const elapsedMs = Math.max(0, nowMs - record.updatedAtMs);
    const refilledTokens = (elapsedMs / options.refillIntervalMs) * options.refillTokens;

    return {
      tokens: Math.min(options.capacity, record.tokens + refilledTokens),
      updatedAtMs: nowMs,
    };
  }
}

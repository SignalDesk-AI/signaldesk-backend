export interface TokenBucketKey {
  key: string;
}

export interface TokenBucketOptions {
  capacity: number;
  refillTokens: number;
  refillIntervalMs: number;
}

export interface TokenBucketState {
  allowed: boolean;
  remainingTokens: number;
  retryAfterMs: number;
  resetAt: string;
}

export interface TokenBucketStore {
  consume(key: TokenBucketKey, options: TokenBucketOptions, nowMs?: number): Promise<TokenBucketState>;
}

export function validateTokenBucketOptions(options: TokenBucketOptions): void {
  if (options.capacity <= 0) {
    throw new Error('Token bucket capacity must be greater than zero.');
  }

  if (options.refillTokens <= 0) {
    throw new Error('Token bucket refillTokens must be greater than zero.');
  }

  if (options.refillIntervalMs <= 0) {
    throw new Error('Token bucket refillIntervalMs must be greater than zero.');
  }
}

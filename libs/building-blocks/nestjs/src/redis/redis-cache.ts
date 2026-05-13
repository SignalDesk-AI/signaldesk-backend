export const REDIS_CACHE = Symbol('REDIS_CACHE');

export interface RedisCacheEntryOptions {
  ttlMs?: number;
}

export interface RedisCache {
  get(key: string): Promise<string | undefined>;
  set(key: string, value: string, options?: RedisCacheEntryOptions): Promise<void>;
  delete(key: string): Promise<void>;
}

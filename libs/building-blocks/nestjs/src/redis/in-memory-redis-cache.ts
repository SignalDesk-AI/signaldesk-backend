import { Injectable } from '@nestjs/common';

import { RedisCache, RedisCacheEntryOptions } from './redis-cache';

interface CacheRecord {
  value: string;
  expiresAtMs?: number;
}

@Injectable()
export class InMemoryRedisCache implements RedisCache {
  private readonly values = new Map<string, CacheRecord>();

  async get(key: string): Promise<string | undefined> {
    const record = this.values.get(key);

    if (!record) {
      return undefined;
    }

    if (record.expiresAtMs !== undefined && record.expiresAtMs <= Date.now()) {
      this.values.delete(key);
      return undefined;
    }

    return record.value;
  }

  async set(key: string, value: string, options: RedisCacheEntryOptions = {}): Promise<void> {
    this.values.set(key, {
      value,
      expiresAtMs: options.ttlMs === undefined ? undefined : Date.now() + options.ttlMs,
    });
  }

  async delete(key: string): Promise<void> {
    this.values.delete(key);
  }
}

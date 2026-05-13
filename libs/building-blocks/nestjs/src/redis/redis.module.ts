import { Module } from '@nestjs/common';

import { InMemoryRedisCache } from './in-memory-redis-cache';
import { REDIS_CACHE } from './redis-cache';

@Module({
  providers: [
    InMemoryRedisCache,
    {
      provide: REDIS_CACHE,
      useExisting: InMemoryRedisCache,
    },
  ],
  exports: [InMemoryRedisCache, REDIS_CACHE],
})
export class RedisModule {}

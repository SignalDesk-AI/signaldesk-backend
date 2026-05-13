import { Module } from '@nestjs/common';
import { InMemoryTokenBucketStore } from '@signaldesk/building-blocks-nestjs';

import { GATEWAY_RATE_LIMIT_BUCKET, GatewayRateLimitGuard } from './gateway-rate-limit.guard';

@Module({
  providers: [
    {
      provide: GATEWAY_RATE_LIMIT_BUCKET,
      useClass: InMemoryTokenBucketStore,
    },
    {
      provide: GatewayRateLimitGuard,
      useFactory: (store: InMemoryTokenBucketStore) => new GatewayRateLimitGuard(store),
      inject: [GATEWAY_RATE_LIMIT_BUCKET],
    },
  ],
  exports: [GatewayRateLimitGuard, GATEWAY_RATE_LIMIT_BUCKET],
})
export class RateLimitModule {}

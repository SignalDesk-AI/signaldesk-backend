import { Module } from '@nestjs/common';
import { APP_FILTER, APP_GUARD } from '@nestjs/core';
import { ErrorEnvelopeFilter, TenantModule } from '@signaldesk/building-blocks-nestjs';

import { GatewayAuthGuard } from './gateway-auth.guard';
import { JwtPlaceholderVerifier } from './jwt-placeholder.verifier';
import { GatewayRateLimitGuard } from '../rate-limit/gateway-rate-limit.guard';
import { RateLimitModule } from '../rate-limit/rate-limit.module';

@Module({
  imports: [TenantModule, RateLimitModule],
  providers: [
    JwtPlaceholderVerifier,
    {
      provide: APP_GUARD,
      useClass: GatewayAuthGuard,
    },
    {
      provide: APP_GUARD,
      useExisting: GatewayRateLimitGuard,
    },
    {
      provide: APP_FILTER,
      useClass: ErrorEnvelopeFilter,
    },
  ],
  exports: [JwtPlaceholderVerifier],
})
export class AuthModule {}

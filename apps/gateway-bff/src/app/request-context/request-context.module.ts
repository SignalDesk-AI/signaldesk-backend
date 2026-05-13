import { MiddlewareConsumer, Module, NestModule } from '@nestjs/common';
import { CorrelationMiddleware, CorrelationModule, TenantModule } from '@signaldesk/building-blocks-nestjs';

import { RequestContextMiddleware } from './request-context.middleware';

export const requestContextRoutePattern = '/{*path}';

@Module({
  imports: [CorrelationModule, TenantModule],
  providers: [RequestContextMiddleware],
})
export class RequestContextModule implements NestModule {
  configure(consumer: MiddlewareConsumer): void {
    consumer.apply(CorrelationMiddleware, RequestContextMiddleware).forRoutes(requestContextRoutePattern);
  }
}

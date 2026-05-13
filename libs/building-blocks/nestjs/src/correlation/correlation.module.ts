import { Module } from '@nestjs/common';

import { CorrelationContextService } from './correlation-context.service';
import { CorrelationMiddleware } from './correlation.middleware';

@Module({
  providers: [CorrelationContextService, CorrelationMiddleware],
  exports: [CorrelationContextService, CorrelationMiddleware],
})
export class CorrelationModule {}

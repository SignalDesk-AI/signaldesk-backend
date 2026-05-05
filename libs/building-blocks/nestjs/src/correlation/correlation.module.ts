import { Module } from '@nestjs/common';

import { CorrelationContextService } from './correlation-context.service';

@Module({
  providers: [CorrelationContextService],
  exports: [CorrelationContextService],
})
export class CorrelationModule {}

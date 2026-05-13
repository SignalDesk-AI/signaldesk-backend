import { Module } from '@nestjs/common';

import { CorrelationModule } from '../correlation';
import { TraceContextService } from './trace-context.service';

@Module({
  imports: [CorrelationModule],
  providers: [TraceContextService],
  exports: [TraceContextService],
})
export class TracingModule {}

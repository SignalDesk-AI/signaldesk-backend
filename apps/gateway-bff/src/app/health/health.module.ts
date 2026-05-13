import { Module } from '@nestjs/common';

import { AggregateHealthController } from './aggregate-health.controller';
import { HealthController } from './health.controller';

@Module({
  controllers: [AggregateHealthController, HealthController],
})
export class HealthModule {}

import { Module } from '@nestjs/common';

import { ConfigValidationModule } from './config';
import { CorrelationModule } from './correlation';
import { HealthModule } from './health';
import { LoggingModule } from './logging';
import { RabbitMqModule } from './rabbitmq';
import { RedisModule } from './redis';
import { TenantModule } from './tenant';
import { TracingModule } from './tracing';

@Module({
  imports: [
    ConfigValidationModule,
    CorrelationModule,
    HealthModule,
    LoggingModule,
    RabbitMqModule,
    RedisModule,
    TenantModule,
    TracingModule,
  ],
  exports: [
    ConfigValidationModule,
    CorrelationModule,
    HealthModule,
    LoggingModule,
    RabbitMqModule,
    RedisModule,
    TenantModule,
    TracingModule,
  ],
})
export class BuildingBlocksNestjsModule {}

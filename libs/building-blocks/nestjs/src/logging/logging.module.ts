import { Module } from '@nestjs/common';

import { CorrelationModule } from '../correlation';
import { TenantModule } from '../tenant';
import { TracingModule } from '../tracing';
import { ConsoleAppLogger } from './console-app-logger';
import { APP_LOGGER } from './logger';

@Module({
  imports: [CorrelationModule, TenantModule, TracingModule],
  providers: [
    ConsoleAppLogger,
    {
      provide: APP_LOGGER,
      useExisting: ConsoleAppLogger,
    },
  ],
  exports: [ConsoleAppLogger, APP_LOGGER],
})
export class LoggingModule {}

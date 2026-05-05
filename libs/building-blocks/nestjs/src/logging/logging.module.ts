import { Module } from '@nestjs/common';

import { ConsoleAppLogger } from './console-app-logger';

@Module({
  providers: [ConsoleAppLogger],
  exports: [ConsoleAppLogger],
})
export class LoggingModule {}

import { Module } from '@nestjs/common';
import { LoggingModule as SharedLoggingModule } from '@signaldesk/building-blocks-nestjs';

@Module({
  imports: [SharedLoggingModule],
  exports: [SharedLoggingModule],
})
export class LoggingModule {}

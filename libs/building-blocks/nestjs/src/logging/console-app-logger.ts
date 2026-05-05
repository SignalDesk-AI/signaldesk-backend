import { Injectable, Logger } from '@nestjs/common';

import { AppLogger, LogContext } from './logger';

@Injectable()
export class ConsoleAppLogger implements AppLogger {
  private readonly logger = new Logger(ConsoleAppLogger.name);

  info(message: string, context?: LogContext): void {
    this.logger.log(this.format(message, context));
  }

  warn(message: string, context?: LogContext): void {
    this.logger.warn(this.format(message, context));
  }

  error(message: string, context?: LogContext): void {
    this.logger.error(this.format(message, context));
  }

  private format(message: string, context?: LogContext): string {
    return JSON.stringify({ message, ...context });
  }
}

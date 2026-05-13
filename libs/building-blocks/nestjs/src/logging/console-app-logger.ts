import { Injectable, Logger } from '@nestjs/common';

import { CorrelationContextService } from '../correlation';
import { TenantContextService } from '../tenant';
import { TraceContextService } from '../tracing';
import { AppLogger, LogContext } from './logger';

@Injectable()
export class ConsoleAppLogger implements AppLogger {
  private readonly logger = new Logger(ConsoleAppLogger.name);

  constructor(
    private readonly correlationContext: CorrelationContextService,
    private readonly tenantContext: TenantContextService,
    private readonly traceContext: TraceContextService
  ) {}

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
    return JSON.stringify({
      message,
      correlationId: this.correlationContext.correlationId,
      tenantId: this.tenantContext.tenantId,
      traceId: this.traceContext.traceId,
      ...context,
    });
  }
}

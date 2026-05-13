export const APP_LOGGER = Symbol('APP_LOGGER');

export interface LogContext {
  correlationId?: string;
  tenantId?: string;
  serviceName?: string;
  traceId?: string;
  [key: string]: unknown;
}

export interface AppLogger {
  info(message: string, context?: LogContext): void;
  warn(message: string, context?: LogContext): void;
  error(message: string, context?: LogContext): void;
}

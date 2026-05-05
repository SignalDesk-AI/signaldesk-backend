export interface LogContext {
  correlationId?: string;
  tenantId?: string;
  serviceName?: string;
}

export interface AppLogger {
  info(message: string, context?: LogContext): void;
  warn(message: string, context?: LogContext): void;
  error(message: string, context?: LogContext): void;
}

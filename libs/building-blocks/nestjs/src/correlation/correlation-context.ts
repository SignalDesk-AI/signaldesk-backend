export const CORRELATION_ID_HEADER = 'x-correlation-id';

export interface CorrelationContext {
  correlationId?: string;
}

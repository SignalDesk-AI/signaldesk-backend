import { CORRELATION_ID_HEADER } from '../correlation';

export const TRACE_ID_HEADER = 'x-trace-id';

export interface TraceContext {
  traceId?: string;
  correlationId?: string;
}

export function extractTraceContext(headers: Record<string, string | string[] | undefined>): TraceContext {
  const traceId = firstHeaderValue(headers[TRACE_ID_HEADER]);
  const correlationId = firstHeaderValue(headers[CORRELATION_ID_HEADER]);

  return {
    traceId,
    correlationId,
  };
}

function firstHeaderValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

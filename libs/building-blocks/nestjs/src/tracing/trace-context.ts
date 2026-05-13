import { CORRELATION_ID_HEADER, normalizeCorrelationId } from '../correlation';

export const TRACE_ID_HEADER = 'x-trace-id';

export interface TraceContext {
  traceId?: string;
  correlationId?: string;
}

export function extractTraceContext(headers: Record<string, string | string[] | undefined>): TraceContext {
  return resolveTraceContext(headers);
}

export function resolveTraceContext(
  headers: Record<string, string | string[] | undefined>,
  fallbackCorrelationId?: string
): TraceContext {
  const traceId = normalizeTraceId(headers[TRACE_ID_HEADER]);
  const correlationId = normalizeCorrelationId(headers[CORRELATION_ID_HEADER]) ?? fallbackCorrelationId;

  return {
    traceId: traceId ?? correlationId,
    correlationId,
  };
}

export function applyTraceHeaders(headers: Record<string, string>, context: TraceContext): Record<string, string> {
  if (context.traceId) {
    headers[TRACE_ID_HEADER] = context.traceId;
  }

  if (context.correlationId) {
    headers[CORRELATION_ID_HEADER] = context.correlationId;
  }

  return headers;
}

function normalizeTraceId(value: string | string[] | undefined): string | undefined {
  const firstValue = Array.isArray(value) ? value[0] : value;
  const normalized = firstValue?.trim();

  return normalized && normalized.length > 0 ? normalized : undefined;
}

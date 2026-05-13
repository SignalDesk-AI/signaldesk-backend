import { randomUUID } from 'node:crypto';

import { CORRELATION_ID_HEADER } from './correlation-context';

export type HeaderValue = string | string[] | undefined;

export function generateCorrelationId(): string {
  return randomUUID();
}

export function normalizeCorrelationId(value: HeaderValue): string | undefined {
  const firstValue = Array.isArray(value) ? value[0] : value;
  const normalized = firstValue?.trim();

  return normalized && normalized.length > 0 ? normalized : undefined;
}

export function resolveCorrelationId(
  headers: Record<string, HeaderValue>,
  fallback: () => string = generateCorrelationId
): string {
  return normalizeCorrelationId(headers[CORRELATION_ID_HEADER]) ?? fallback();
}

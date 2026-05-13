export type ErrorCode =
  | 'VALIDATION_ERROR'
  | 'UNAUTHENTICATED'
  | 'FORBIDDEN'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'RATE_LIMITED'
  | 'INTERNAL_ERROR';

export interface ErrorEnvelopeError {
  code: ErrorCode | string;
  message: string;
  details: unknown[];
  correlationId?: string;
}

export interface ErrorEnvelope {
  error: ErrorEnvelopeError;
}

export interface ErrorEnvelopeInput {
  code: ErrorCode | string;
  message: string;
  details?: unknown[];
  correlationId?: string;
}

export function createErrorEnvelope(input: ErrorEnvelopeInput): ErrorEnvelope {
  return {
    error: {
      code: input.code,
      message: input.message,
      details: input.details ?? [],
      correlationId: input.correlationId,
    },
  };
}

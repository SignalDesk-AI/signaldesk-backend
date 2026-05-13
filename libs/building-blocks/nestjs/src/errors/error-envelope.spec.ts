import { BadRequestException, HttpStatus } from '@nestjs/common';

import { ApiException } from './api-exception';
import { errorCodeForStatus } from './error-code';
import { createErrorEnvelope } from './error-envelope';
import { ErrorEnvelopeFilter } from './error-envelope.filter';

describe('error envelope helpers', () => {
  it('creates the documented error envelope without top-level data', () => {
    const envelope = createErrorEnvelope({
      code: 'VALIDATION_ERROR',
      message: 'Request is invalid',
      details: [{ path: 'name' }],
      correlationId: 'correlation-1',
    });

    expect(envelope).toEqual({
      error: {
        code: 'VALIDATION_ERROR',
        message: 'Request is invalid',
        details: [{ path: 'name' }],
        correlationId: 'correlation-1',
      },
    });
    expect(envelope).not.toHaveProperty('data');
  });

  it('maps common HTTP status values to API error codes', () => {
    expect(errorCodeForStatus(HttpStatus.FORBIDDEN)).toBe('FORBIDDEN');
    expect(errorCodeForStatus(HttpStatus.TOO_MANY_REQUESTS)).toBe('RATE_LIMITED');
    expect(errorCodeForStatus(418)).toBe('INTERNAL_ERROR');
  });

  it('writes an ApiException as an error envelope', () => {
    const response = createResponse();
    const filter = new ErrorEnvelopeFilter();

    filter.catch(
      new ApiException('FORBIDDEN', 'Tenant mismatch', HttpStatus.FORBIDDEN, [
        { path: 'tenantId' },
      ]),
      createHost(response, 'correlation-1')
    );

    expect(response.statusCode).toBe(HttpStatus.FORBIDDEN);
    expect(response.body).toEqual({
      error: {
        code: 'FORBIDDEN',
        message: 'Tenant mismatch',
        details: [{ path: 'tenantId' }],
        correlationId: 'correlation-1',
      },
    });
  });

  it('normalizes Nest validation errors', () => {
    const response = createResponse();
    const filter = new ErrorEnvelopeFilter();

    filter.catch(new BadRequestException(['name is required']), createHost(response));

    expect(response.statusCode).toBe(HttpStatus.BAD_REQUEST);
    expect(response.body).toEqual({
      error: {
        code: 'VALIDATION_ERROR',
        message: 'Request is invalid',
        details: ['name is required'],
        correlationId: undefined,
      },
    });
  });
});

function createResponse(): { statusCode?: number; body?: unknown; status: (code: number) => { json: (body: unknown) => void } } {
  return {
    status(code: number) {
      this.statusCode = code;
      return {
        json: (body: unknown) => {
          this.body = body;
        },
      };
    },
  };
}

function createHost(response: unknown, correlationId?: string) {
  return {
    switchToHttp: () => ({
      getResponse: () => response,
      getRequest: () => ({
        headers: correlationId ? { 'x-correlation-id': correlationId } : {},
      }),
    }),
  } as never;
}

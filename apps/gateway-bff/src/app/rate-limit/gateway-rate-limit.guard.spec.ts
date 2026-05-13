import { ExecutionContext, HttpStatus } from '@nestjs/common';
import { ApiException, TokenBucketStore } from '@signaldesk/building-blocks-nestjs';

import { GatewayRateLimitGuard, gatewayRateLimitKey } from './gateway-rate-limit.guard';

describe('GatewayRateLimitGuard', () => {
  it('uses trusted auth tenant ID for authenticated requests', () => {
    expect(
      gatewayRateLimitKey({
        method: 'GET',
        url: '/api/support/tickets',
        headers: { 'x-tenant-id': 'client-tenant' },
        auth: {
          userId: 'user-1',
          tenantId: 'trusted-tenant',
          roles: [],
          permissions: [],
        },
      })
    ).toBe('tenant:trusted-tenant');
  });

  it('does not trust client tenant header for public request keys', () => {
    expect(
      gatewayRateLimitKey({
        method: 'POST',
        url: '/api/auth/login',
        headers: {
          'x-tenant-id': 'client-tenant',
          'x-forwarded-for': '203.0.113.10, 10.0.0.1',
        },
      })
    ).toBe('public:auth:login:203.0.113.10');
  });

  it('falls back to socket address for public request keys', () => {
    expect(
      gatewayRateLimitKey({
        method: 'GET',
        url: '/health/live?verbose=true',
        headers: {},
        socket: { remoteAddress: '127.0.0.1' },
      })
    ).toBe('public:health-live:127.0.0.1');
  });

  it('allows requests while bucket tokens remain and writes rate limit headers', async () => {
    const store: TokenBucketStore = {
      consume: jest.fn().mockResolvedValue({
        allowed: true,
        remainingTokens: 12,
        retryAfterMs: 0,
        resetAt: new Date(0).toISOString(),
      }),
    };
    const guard = new GatewayRateLimitGuard(store);
    const response = createResponse();

    await expect(
      guard.canActivate(
        createExecutionContext(
          {
            method: 'GET',
            url: '/api/support/tickets',
            headers: {},
            auth: {
              userId: 'user-1',
              tenantId: 'tenant-1',
              roles: [],
              permissions: [],
            },
          },
          response
        )
      )
    ).resolves.toBe(true);

    expect(response.headers['X-RateLimit-Limit']).toBe('60');
    expect(response.headers['X-RateLimit-Remaining']).toBe('12');
  });

  it('throws a documented RATE_LIMITED error when the bucket is empty', async () => {
    const store: TokenBucketStore = {
      consume: jest.fn().mockResolvedValue({
        allowed: false,
        remainingTokens: 0,
        retryAfterMs: 1000,
        resetAt: new Date(1000).toISOString(),
      }),
    };
    const guard = new GatewayRateLimitGuard(store);
    const response = createResponse();

    let exception: unknown;

    try {
      await guard.canActivate(
        createExecutionContext(
          {
            method: 'GET',
            url: '/api/support/tickets',
            headers: {},
            auth: {
              userId: 'user-1',
              tenantId: 'tenant-1',
              roles: [],
              permissions: [],
            },
          },
          response
        )
      );
    } catch (error) {
      exception = error;
    }

    expect(exception).toBeInstanceOf(ApiException);
    expect(exception).toMatchObject({
      code: 'RATE_LIMITED',
      message: 'Rate limit exceeded',
    } satisfies Partial<ApiException>);
    expect((exception as ApiException).getStatus()).toBe(HttpStatus.TOO_MANY_REQUESTS);
    expect(response.headers['Retry-After']).toBe('1');
  });

  it('uses the injected token bucket store', async () => {
    const store: TokenBucketStore = {
      consume: jest.fn().mockResolvedValue({
        allowed: true,
        remainingTokens: 12,
        retryAfterMs: 0,
        resetAt: new Date(0).toISOString(),
      }),
    };
    const guard = new GatewayRateLimitGuard(store);

    await expect(
      guard.canActivate(createExecutionContext({ method: 'GET', url: '/api/health', headers: {} }))
    ).resolves.toBe(true);
    expect(store.consume).toHaveBeenCalledWith(
      { key: 'public:api-health:unknown' },
      expect.objectContaining({ capacity: 60 })
    );
  });
});

function createResponse(): { headers: Record<string, string>; setHeader(name: string, value: string): void } {
  return {
    headers: {},
    setHeader(name: string, value: string): void {
      this.headers[name] = value;
    },
  };
}

function createExecutionContext(request: unknown, response = createResponse()): ExecutionContext {
  return {
    switchToHttp: () => ({
      getRequest: () => request,
      getResponse: () => response,
    }),
  } as ExecutionContext;
}

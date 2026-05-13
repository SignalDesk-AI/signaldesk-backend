import { ExecutionContext, UnauthorizedException } from '@nestjs/common';
import { TenantContextService, TenantMismatchException } from '@signaldesk/building-blocks-nestjs';

import { GatewayAuthGuard } from './gateway-auth.guard';
import { JwtPlaceholderVerifier } from './jwt-placeholder.verifier';

describe('GatewayAuthGuard', () => {
  it('allows public routes without auth', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('GET', '/health/live');

    expect(runGuard(tenantContext, guard, request).allowed).toBe(true);
    expect(request.auth).toBeUndefined();
  });

  it('fails closed for protected routes without auth', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);

    expect(() => runGuard(tenantContext, guard, createRequest('GET', '/api/support'))).toThrow(
      UnauthorizedException
    );
  });

  it('resolves auth and tenant context from token claims for protected routes', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('GET', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
    });
    const response = createResponse();

    const result = runGuard(tenantContext, guard, request, response);

    expect(result.allowed).toBe(true);
    expect(result.tenantId).toBe('tenant-1');
    expect(request.auth).toEqual({
      userId: 'user-1',
      tenantId: 'tenant-1',
      roles: [],
      permissions: [],
    });
    expect(request.headers['x-tenant-id']).toBe('tenant-1');
    expect(response.headers['x-tenant-id']).toBe('tenant-1');
  });

  it('allows missing optional client tenant IDs', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('GET', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
    });

    expect(runGuard(tenantContext, guard, request).allowed).toBe(true);
  });

  it('allows all provided client tenant IDs when they match trusted tenant', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('GET', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
      'x-tenant-id': 'tenant-1',
    });
    request.params = { tenantId: 'tenant-1' };
    request.query = { tenantId: 'tenant-1' };
    request.body = { tenantId: 'tenant-1' };

    expect(runGuard(tenantContext, guard, request).allowed).toBe(true);
    expect(request.headers['x-tenant-id']).toBe('tenant-1');
  });

  it('rejects when header matches but body differs', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('POST', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
      'x-tenant-id': 'tenant-1',
    });
    request.body = { tenantId: 'tenant-2' };

    expect(() => runGuard(tenantContext, guard, request)).toThrow(TenantMismatchException);
  });

  it('rejects when header matches but query differs', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('GET', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
      'x-tenant-id': 'tenant-1',
    });
    request.query = { tenantId: 'tenant-2' };

    expect(() => runGuard(tenantContext, guard, request)).toThrow(TenantMismatchException);
  });

  it('rejects body-only tenant mismatches', () => {
    const tenantContext = initializedTenantContext();
    const guard = new GatewayAuthGuard(new JwtPlaceholderVerifier(), tenantContext);
    const request = createRequest('POST', '/api/support', {
      authorization: `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1' })}`,
    });
    request.body = { tenantId: 'tenant-2' };

    expect(() => runGuard(tenantContext, guard, request)).toThrow(TenantMismatchException);
  });
});

function runGuard(
  tenantContext: TenantContextService,
  guard: GatewayAuthGuard,
  request: unknown,
  response = createResponse()
): { allowed: boolean; tenantId?: string } {
  return tenantContext.run({ tenantId: undefined }, () => ({
    allowed: guard.canActivate(createExecutionContext(request, response)),
    tenantId: tenantContext.tenantId,
  }));
}

function initializedTenantContext(): TenantContextService {
  return new TenantContextService();
}

function createRequest(
  method: string,
  url: string,
  headers: Record<string, string> = {}
): {
  method: string;
  url: string;
  originalUrl: string;
  headers: Record<string, string | string[] | undefined>;
  params?: Record<string, unknown>;
  query?: Record<string, unknown>;
  body?: unknown;
  auth?: unknown;
} {
  return {
    method,
    url,
    originalUrl: url,
    headers,
  };
}

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

function jwt(payload: object): string {
  return [base64Url({ alg: 'none', typ: 'JWT' }), base64Url(payload), 'signature'].join('.');
}

function base64Url(value: object): string {
  return Buffer.from(JSON.stringify(value))
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

import { gatewayRouteMap } from './gateway-route-map';
import { buildDownstreamUrl, buildForwardHeaders, buildProxyBody, rewriteDownstreamPath } from './proxy-request';

describe('proxy request helpers', () => {
  it('rewrites support gateway paths to downstream paths without the global api prefix', () => {
    const route = gatewayRouteMap.find((item) => item.prefix === '/api/support');

    expect(route).toBeDefined();
    expect(
      rewriteDownstreamPath(route!, {
        method: 'GET',
        originalUrl: '/api/support/tickets?page=1',
        url: '/api/support/tickets?page=1',
        headers: {},
      })
    ).toBe('/support/tickets?page=1');
    expect(
      buildDownstreamUrl(route!, {
        method: 'GET',
        originalUrl: '/api/support/tickets?page=1',
        url: '/api/support/tickets?page=1',
        headers: {},
      })
    ).toBe('http://localhost:5003/support/tickets?page=1');
  });

  it('rewrites auth gateway paths to identity downstream paths', () => {
    const route = gatewayRouteMap.find((item) => item.prefix === '/api/auth');

    expect(route).toBeDefined();
    expect(
      rewriteDownstreamPath(route!, {
        method: 'POST',
        originalUrl: '/api/auth/login',
        url: '/api/auth/login',
        headers: {},
      })
    ).toBe('/auth/login');
    expect(
      buildDownstreamUrl(route!, {
        method: 'POST',
        originalUrl: '/api/auth/login',
        url: '/api/auth/login',
        headers: {},
      })
    ).toBe('http://localhost:5001/auth/login');
  });

  it('forwards correlation ID, trusted tenant ID, authorization, and content type only', () => {
    expect(
      buildForwardHeaders({
        method: 'POST',
        url: '/api/support/tickets',
        headers: {
          'x-correlation-id': 'correlation-1',
          'x-tenant-id': 'tenant-1',
          authorization: 'Bearer token',
          'content-type': 'application/json',
          host: 'localhost:3000',
        },
      })
    ).toEqual({
      'x-correlation-id': 'correlation-1',
      'x-tenant-id': 'tenant-1',
      authorization: 'Bearer token',
      'content-type': 'application/json',
    });
  });

  it('omits request bodies for GET and HEAD requests', () => {
    expect(buildProxyBody({ method: 'GET', url: '/api/support', headers: {}, body: { ok: true } })).toBeUndefined();
    expect(buildProxyBody({ method: 'HEAD', url: '/api/support', headers: {}, body: { ok: true } })).toBeUndefined();
  });
});

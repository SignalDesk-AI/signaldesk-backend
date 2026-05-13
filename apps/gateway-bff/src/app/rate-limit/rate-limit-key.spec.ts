import { publicRouteGroup } from './rate-limit-key';

describe('publicRouteGroup', () => {
  it('groups health and aggregate health public routes without query strings', () => {
    expect(publicRouteGroup('GET', '/health/live?verbose=true')).toBe('health-live');
    expect(publicRouteGroup('GET', '/health/ready')).toBe('health-ready');
    expect(publicRouteGroup('GET', '/api/health')).toBe('api-health');
  });

  it('groups public auth entrypoints by route name', () => {
    expect(publicRouteGroup('POST', '/api/auth/login')).toBe('auth:login');
    expect(publicRouteGroup('POST', '/api/auth/register')).toBe('auth:register');
    expect(publicRouteGroup('POST', '/api/auth/refresh')).toBe('auth:refresh');
  });

  it('uses method and normalized path for unexpected public keys', () => {
    expect(publicRouteGroup('GET', '/api/auth/login/')).toBe('GET:/api/auth/login');
  });
});

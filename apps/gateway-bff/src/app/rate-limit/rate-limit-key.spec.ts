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

  it('groups email verification and password reset public routes', () => {
    expect(publicRouteGroup('POST', '/api/auth/verify-email')).toBe('auth:verify-email');
    expect(publicRouteGroup('POST', '/api/auth/password-reset/request')).toBe('auth:password-reset/request');
    expect(publicRouteGroup('POST', '/api/auth/password-reset/confirm')).toBe('auth:password-reset/confirm');
  });

  it('uses method and normalized path for unexpected public keys', () => {
    expect(publicRouteGroup('GET', '/api/auth/login/')).toBe('GET:/api/auth/login');
  });
});

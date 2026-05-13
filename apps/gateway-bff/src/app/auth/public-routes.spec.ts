import { isPublicRoute } from './public-routes';

describe('isPublicRoute', () => {
  it('allows health endpoints without auth', () => {
    expect(isPublicRoute('GET', '/health/live')).toBe(true);
    expect(isPublicRoute('GET', '/health/ready')).toBe(true);
    expect(isPublicRoute('GET', '/api/health')).toBe(true);
  });

  it('allows configured auth entrypoints', () => {
    expect(isPublicRoute('POST', '/api/auth/register')).toBe(true);
    expect(isPublicRoute('POST', '/api/auth/login')).toBe(true);
    expect(isPublicRoute('POST', '/api/auth/refresh')).toBe(true);
  });

  it('protects auth entrypoints with unexpected methods and suffix paths', () => {
    expect(isPublicRoute('GET', '/api/auth/login')).toBe(false);
    expect(isPublicRoute('POST', '/api/auth/login/extra')).toBe(false);
    expect(isPublicRoute('POST', '/api/auth/register/extra')).toBe(false);
    expect(isPublicRoute('POST', '/api/auth/refresh/extra')).toBe(false);
  });

  it('keeps downstream API routes protected', () => {
    expect(isPublicRoute('GET', '/api/support/tickets')).toBe(false);
    expect(isPublicRoute('GET', '/api/workspace')).toBe(false);
  });
});

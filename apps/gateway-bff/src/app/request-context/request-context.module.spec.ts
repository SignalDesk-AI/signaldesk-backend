import { requestContextRoutePattern } from './request-context.module';

describe('RequestContextModule', () => {
  it('uses a Nest 11 compatible wildcard route pattern', () => {
    expect(requestContextRoutePattern).toBe('/{*path}');
  });
});

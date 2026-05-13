import { gatewayRouteMap, resolveRoute } from './gateway-route-map';

describe('gatewayRouteMap', () => {
  it('maps documented gateway prefixes to downstream services and path prefixes', () => {
    expect(gatewayRouteMap.map((route) => [route.prefix, route.downstreamPrefix, route.serviceName])).toEqual([
      ['/api/auth', '/auth', 'identity-service'],
      ['/api/users', '/users', 'identity-service'],
      ['/api/workspace', '/workspace', 'workspace-service'],
      ['/api/support', '/support', 'support-service'],
      ['/api/knowledge', '/knowledge', 'knowledge-service'],
      ['/api/notifications', '/notifications', 'notification-service'],
      ['/api/search', '/search', 'search-service'],
      ['/api/ai', '/ai', 'ai-service'],
      ['/api/campaigns', '/campaigns', 'campaign-service'],
    ]);
  });

  it('resolves exact and nested prefix routes', () => {
    expect(resolveRoute('/api/support')?.serviceName).toBe('support-service');
    expect(resolveRoute('/api/support/tickets')?.serviceName).toBe('support-service');
    expect(resolveRoute('/api/supporting')).toBeUndefined();
  });
});

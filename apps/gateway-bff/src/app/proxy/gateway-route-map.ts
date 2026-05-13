export interface GatewayRoute {
  prefix: string;
  downstreamPrefix: string;
  serviceName: string;
  envName: string;
  fallbackUrl: string;
}

export const gatewayRouteMap: readonly GatewayRoute[] = [
  {
    prefix: '/api/auth',
    downstreamPrefix: '/auth',
    serviceName: 'identity-service',
    envName: 'IDENTITY_SERVICE_URL',
    fallbackUrl: 'http://localhost:5001',
  },
  {
    prefix: '/api/users',
    downstreamPrefix: '/users',
    serviceName: 'identity-service',
    envName: 'IDENTITY_SERVICE_URL',
    fallbackUrl: 'http://localhost:5001',
  },
  {
    prefix: '/api/workspace',
    downstreamPrefix: '/workspace',
    serviceName: 'workspace-service',
    envName: 'WORKSPACE_SERVICE_URL',
    fallbackUrl: 'http://localhost:5002',
  },
  {
    prefix: '/api/support',
    downstreamPrefix: '/support',
    serviceName: 'support-service',
    envName: 'SUPPORT_SERVICE_URL',
    fallbackUrl: 'http://localhost:5003',
  },
  {
    prefix: '/api/knowledge',
    downstreamPrefix: '/knowledge',
    serviceName: 'knowledge-service',
    envName: 'KNOWLEDGE_SERVICE_URL',
    fallbackUrl: 'http://localhost:5004',
  },
  {
    prefix: '/api/notifications',
    downstreamPrefix: '/notifications',
    serviceName: 'notification-service',
    envName: 'NOTIFICATION_SERVICE_URL',
    fallbackUrl: 'http://localhost:3001',
  },
  {
    prefix: '/api/search',
    downstreamPrefix: '/search',
    serviceName: 'search-service',
    envName: 'SEARCH_SERVICE_URL',
    fallbackUrl: 'http://localhost:3002',
  },
  {
    prefix: '/api/ai',
    downstreamPrefix: '/ai',
    serviceName: 'ai-service',
    envName: 'AI_SERVICE_URL',
    fallbackUrl: 'http://localhost:3003',
  },
  {
    prefix: '/api/campaigns',
    downstreamPrefix: '/campaigns',
    serviceName: 'campaign-service',
    envName: 'CAMPAIGN_SERVICE_URL',
    fallbackUrl: 'http://localhost:5005',
  },
];

export function resolveRoute(path: string): GatewayRoute | undefined {
  return gatewayRouteMap.find(
    (route) => path === route.prefix || path.startsWith(`${route.prefix}/`)
  );
}

export function downstreamBaseUrl(route: GatewayRoute): string {
  return process.env[route.envName] ?? route.fallbackUrl;
}

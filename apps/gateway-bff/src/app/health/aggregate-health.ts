import { gatewayRouteMap } from '../proxy/gateway-route-map';

const serviceName = 'gateway-bff';

export const downstreamUrls = Array.from(
  new Map(
    gatewayRouteMap.map((route) => [
      route.serviceName,
      [route.serviceName, route.envName, route.fallbackUrl] as const,
    ])
  ).values()
);

export function aggregateHealthPlaceholder() {
  return {
    status: 'ok',
    service: serviceName,
    checkedAt: new Date().toISOString(),
    downstream: downstreamUrls.map(([service, envName, fallback]) => ({
      service,
      status: 'not_checked',
      target: process.env[envName] ?? fallback,
    })),
  };
}

export { serviceName };

import { RequestMethod } from '@nestjs/common';

export interface RouteRule {
  path: string;
  method?: RequestMethod;
  match?: 'exact' | 'prefix';
}

export const publicRouteRules: readonly RouteRule[] = [
  { path: '/health/live' },
  { path: '/health/ready' },
  { path: '/api/health', method: RequestMethod.GET },
  { path: '/api/auth/register', method: RequestMethod.POST },
  { path: '/api/auth/login', method: RequestMethod.POST },
  { path: '/api/auth/refresh', method: RequestMethod.POST },
  { path: '/api/auth/verify-email', method: RequestMethod.POST },
  { path: '/api/auth/password-reset/request', method: RequestMethod.POST },
  { path: '/api/auth/password-reset/confirm', method: RequestMethod.POST },
];

export function isPublicRoute(method: string, path: string): boolean {
  const normalizedPath = normalizePath(path);
  const requestMethod = method.toUpperCase();

  return publicRouteRules.some((rule) => {
    const methodMatches =
      rule.method === undefined || RequestMethod[rule.method] === requestMethod;
    return methodMatches && routePathMatches(rule, normalizedPath);
  });
}

function routePathMatches(rule: RouteRule, normalizedPath: string): boolean {
  if (rule.match === 'prefix') {
    return normalizedPath === rule.path || normalizedPath.startsWith(`${rule.path}/`);
  }

  return normalizedPath === rule.path;
}

function normalizePath(path: string): string {
  const [withoutQuery] = path.split('?');
  const normalized = withoutQuery.endsWith('/') && withoutQuery.length > 1
    ? withoutQuery.slice(0, -1)
    : withoutQuery;

  return normalized.length > 0 ? normalized : '/';
}

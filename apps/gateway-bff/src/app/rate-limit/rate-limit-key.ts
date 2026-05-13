export function publicRouteGroup(method: string, path: string): string {
  const normalizedPath = normalizePath(path);

  if (normalizedPath === '/health/live') {
    return 'health-live';
  }

  if (normalizedPath === '/health/ready') {
    return 'health-ready';
  }

  if (normalizedPath === '/api/health') {
    return 'api-health';
  }

  if (method.toUpperCase() === 'POST' && normalizedPath.startsWith('/api/auth/')) {
    return `auth:${normalizedPath.slice('/api/auth/'.length)}`;
  }

  return `${method.toUpperCase()}:${normalizedPath}`;
}

function normalizePath(path: string): string {
  const [withoutQuery] = path.split('?');
  const normalized = withoutQuery.endsWith('/') && withoutQuery.length > 1
    ? withoutQuery.slice(0, -1)
    : withoutQuery;

  return normalized.length > 0 ? normalized : '/';
}

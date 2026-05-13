export interface ClientTenantCandidate {
  source: 'header' | 'params' | 'query' | 'body';
  tenantId: string;
}

export function findClientProvidedTenantIds(request: {
  headers: Record<string, string | string[] | undefined>;
  params?: Record<string, unknown>;
  query?: Record<string, unknown>;
  body?: unknown;
}): ClientTenantCandidate[] {
  return [
    ...tenantCandidates('header', request.headers['x-tenant-id']),
    ...tenantCandidates('params', request.params?.['tenantId']),
    ...tenantCandidates('query', request.query?.['tenantId']),
    ...tenantCandidates('body', bodyTenantId(request.body)),
  ];
}

export function findClientProvidedTenantId(request: {
  headers: Record<string, string | string[] | undefined>;
  params?: Record<string, unknown>;
  query?: Record<string, unknown>;
  body?: unknown;
}): string | undefined {
  return findClientProvidedTenantIds(request)[0]?.tenantId;
}

function bodyTenantId(body: unknown): unknown {
  if (!body || typeof body !== 'object') {
    return undefined;
  }

  return (body as Record<string, unknown>)['tenantId'];
}

function tenantCandidates(
  source: ClientTenantCandidate['source'],
  value: unknown
): ClientTenantCandidate[] {
  return normalizeStrings(value).map((tenantId) => ({ source, tenantId }));
}

function normalizeStrings(value: unknown): string[] {
  const values = Array.isArray(value) ? value : [value];

  return values.filter((candidate): candidate is string =>
    typeof candidate === 'string' && candidate.trim().length > 0
  ).map((candidate) => candidate.trim());
}

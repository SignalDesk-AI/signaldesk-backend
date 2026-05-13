import { CORRELATION_ID_HEADER, TENANT_ID_HEADER } from '@signaldesk/building-blocks-nestjs';

import { GatewayRoute, downstreamBaseUrl } from './gateway-route-map';

export interface ProxyRequest {
  method: string;
  originalUrl?: string;
  url: string;
  headers: Record<string, string | string[] | undefined>;
  body?: unknown;
}

export interface ProxyResponse {
  statusCode: number;
  headers: Record<string, string>;
  body: unknown;
}

export function buildDownstreamUrl(route: GatewayRoute, request: ProxyRequest): string {
  return `${downstreamBaseUrl(route).replace(/\/$/, '')}${rewriteDownstreamPath(route, request)}`;
}

export function rewriteDownstreamPath(route: GatewayRoute, request: ProxyRequest): string {
  const requestUrl = request.originalUrl ?? request.url;
  const pathAndQuery = requestUrl.startsWith('/') ? requestUrl : `/${requestUrl}`;
  const [path, query = ''] = pathAndQuery.split('?');
  const suffix = path.slice(route.prefix.length);
  const downstreamPath = `${route.downstreamPrefix}${suffix}` || '/';

  return query.length > 0 ? `${downstreamPath}?${query}` : downstreamPath;
}

export function buildForwardHeaders(request: ProxyRequest): Record<string, string> {
  const headers: Record<string, string> = {};
  const correlationId = firstHeaderValue(request.headers[CORRELATION_ID_HEADER]);
  const tenantId = firstHeaderValue(request.headers[TENANT_ID_HEADER]);
  const authorization = firstHeaderValue(request.headers['authorization']);
  const contentType = firstHeaderValue(request.headers['content-type']);

  if (correlationId) {
    headers[CORRELATION_ID_HEADER] = correlationId;
  }

  if (tenantId) {
    headers[TENANT_ID_HEADER] = tenantId;
  }

  if (authorization) {
    headers.authorization = authorization;
  }

  if (contentType) {
    headers['content-type'] = contentType;
  }

  return headers;
}

export function buildProxyBody(request: ProxyRequest): BodyInit | undefined {
  if (request.method === 'GET' || request.method === 'HEAD' || request.body === undefined) {
    return undefined;
  }

  return typeof request.body === 'string' ? request.body : JSON.stringify(request.body);
}

export async function readProxyResponse(response: Response): Promise<ProxyResponse> {
  const text = await response.text();
  const headers: Record<string, string> = {};

  response.headers.forEach((value, name) => {
    headers[name] = value;
  });

  return {
    statusCode: response.status,
    headers,
    body: text.length > 0 ? parseBody(text) : undefined,
  };
}

function parseBody(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function firstHeaderValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

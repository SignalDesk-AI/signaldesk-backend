import { CanActivate, ExecutionContext, HttpStatus, Injectable } from '@nestjs/common';
import { ApiException, TokenBucketStore } from '@signaldesk/building-blocks-nestjs';

import { AuthenticatedRequest } from '../auth/auth-claim';
import { publicRouteGroup } from './rate-limit-key';

export const GATEWAY_RATE_LIMIT_BUCKET = Symbol('GATEWAY_RATE_LIMIT_BUCKET');

export const gatewayRateLimitOptions = {
  capacity: 60,
  refillTokens: 60,
  refillIntervalMs: 60_000,
};

interface RateLimitRequest extends AuthenticatedRequest {
  method: string;
  originalUrl?: string;
  url: string;
  ip?: string;
  socket?: { remoteAddress?: string };
}

interface RateLimitResponse {
  setHeader(name: string, value: string): void;
}

@Injectable()
export class GatewayRateLimitGuard implements CanActivate {
  constructor(
    private readonly store: TokenBucketStore
  ) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const http = context.switchToHttp();
    const request = http.getRequest<RateLimitRequest>();
    const response = http.getResponse<RateLimitResponse>();
    const state = await this.store.consume(
      { key: gatewayRateLimitKey(request) },
      gatewayRateLimitOptions
    );

    response.setHeader('X-RateLimit-Limit', gatewayRateLimitOptions.capacity.toString());
    response.setHeader('X-RateLimit-Remaining', state.remainingTokens.toString());
    response.setHeader('X-RateLimit-Reset', state.resetAt);

    if (!state.allowed) {
      response.setHeader('Retry-After', Math.ceil(state.retryAfterMs / 1000).toString());
      throw new ApiException('RATE_LIMITED', 'Rate limit exceeded', HttpStatus.TOO_MANY_REQUESTS, [
        {
          retryAfterMs: state.retryAfterMs,
          resetAt: state.resetAt,
        },
      ]);
    }

    return true;
  }
}

export function gatewayRateLimitKey(request: RateLimitRequest): string {
  const routePath = request.originalUrl ?? request.url;

  if (request.auth?.tenantId) {
    return `tenant:${request.auth.tenantId}`;
  }

  return `public:${publicRouteGroup(request.method, routePath)}:${clientAddress(request)}`;
}

function clientAddress(request: RateLimitRequest): string {
  return firstHeaderValue(request.headers['x-forwarded-for'])
    ?.split(',')[0]
    ?.trim()
    || firstHeaderValue(request.headers['x-real-ip'])
    || request.ip
    || request.socket?.remoteAddress
    || 'unknown';
}

function firstHeaderValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

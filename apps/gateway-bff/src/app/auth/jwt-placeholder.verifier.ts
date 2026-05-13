import { Injectable, UnauthorizedException } from '@nestjs/common';

import { AuthClaims } from './auth-claim';

interface JwtPayload {
  sub?: unknown;
  userId?: unknown;
  tenantId?: unknown;
  tid?: unknown;
  roles?: unknown;
  permissions?: unknown;
  exp?: unknown;
}

@Injectable()
export class JwtPlaceholderVerifier {
  verifyAuthorizationHeader(authorization: string | undefined): AuthClaims {
    const token = this.extractBearerToken(authorization);
    const payload = this.decodePayload(token);
    const userId = stringClaim(payload.userId) ?? stringClaim(payload.sub);
    const tenantId = stringClaim(payload.tenantId) ?? stringClaim(payload.tid);

    if (!userId || !tenantId) {
      throw new UnauthorizedException('Access token is missing required claims');
    }

    if (typeof payload.exp === 'number' && payload.exp * 1000 <= Date.now()) {
      throw new UnauthorizedException('Access token is expired');
    }

    return {
      userId,
      tenantId,
      roles: stringArrayClaim(payload.roles),
      permissions: stringArrayClaim(payload.permissions),
    };
  }

  private extractBearerToken(authorization: string | undefined): string {
    if (!authorization) {
      throw new UnauthorizedException('Missing access token');
    }

    const [scheme, token] = authorization.split(' ');

    if (scheme !== 'Bearer' || !token) {
      throw new UnauthorizedException('Invalid authorization header');
    }

    return token;
  }

  private decodePayload(token: string): JwtPayload {
    const parts = token.split('.');

    if (parts.length !== 3) {
      throw new UnauthorizedException('Invalid access token');
    }

    try {
      return JSON.parse(Buffer.from(base64UrlToBase64(parts[1]), 'base64').toString('utf8')) as JwtPayload;
    } catch {
      throw new UnauthorizedException('Invalid access token');
    }
  }
}

function base64UrlToBase64(value: string): string {
  return value.replace(/-/g, '+').replace(/_/g, '/');
}

function stringClaim(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
}

function stringArrayClaim(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter((item): item is string => typeof item === 'string' && item.length > 0);
}

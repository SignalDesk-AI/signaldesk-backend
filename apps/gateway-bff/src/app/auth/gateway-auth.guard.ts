import { CanActivate, ExecutionContext, Injectable } from '@nestjs/common';
import {
  assertOptionalTenantMatch,
  TenantContextService,
  TENANT_ID_HEADER,
} from '@signaldesk/building-blocks-nestjs';

import { AuthenticatedRequest } from './auth-claim';
import { JwtPlaceholderVerifier } from './jwt-placeholder.verifier';
import { isPublicRoute } from './public-routes';
import { findClientProvidedTenantIds } from '../request-context/tenant-candidate';

interface HttpResponse {
  setHeader(name: string, value: string): void;
}

@Injectable()
export class GatewayAuthGuard implements CanActivate {
  constructor(
    private readonly jwtVerifier: JwtPlaceholderVerifier,
    private readonly tenantContext: TenantContextService
  ) {}

  canActivate(context: ExecutionContext): boolean {
    const http = context.switchToHttp();
    const request = http.getRequest<AuthenticatedRequest & { method: string; originalUrl?: string; url: string }>();
    const response = http.getResponse<HttpResponse>();
    const path = request.originalUrl ?? request.url;

    if (isPublicRoute(request.method, path)) {
      return true;
    }

    const auth = this.jwtVerifier.verifyAuthorizationHeader(
      firstHeaderValue(request.headers['authorization'])
    );
    for (const candidate of findClientProvidedTenantIds(request)) {
      assertOptionalTenantMatch(auth.tenantId, candidate.tenantId);
    }

    request.auth = auth;
    request.headers[TENANT_ID_HEADER] = auth.tenantId;
    response.setHeader(TENANT_ID_HEADER, auth.tenantId);
    this.tenantContext.setTenantId(auth.tenantId);

    return true;
  }
}

function firstHeaderValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

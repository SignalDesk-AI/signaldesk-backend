import { Injectable, NestMiddleware } from '@nestjs/common';
import { TenantContextService, TENANT_ID_HEADER } from '@signaldesk/building-blocks-nestjs';

import { AuthenticatedRequest } from '../auth/auth-claim';

interface HeaderResponse {
  setHeader(name: string, value: string): void;
}

@Injectable()
export class RequestContextMiddleware implements NestMiddleware {
  constructor(private readonly tenantContext: TenantContextService) {}

  use(request: AuthenticatedRequest, response: HeaderResponse, next: () => void): void {
    this.tenantContext.run({ tenantId: undefined }, () => {
      const tenantId = this.tenantContext.tenantId;

      if (tenantId) {
        request.headers[TENANT_ID_HEADER] = tenantId;
        response.setHeader(TENANT_ID_HEADER, tenantId);
      }

      next();
    });
  }
}

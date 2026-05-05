import { Injectable } from '@nestjs/common';

import { TenantContext } from './tenant-context';

@Injectable()
export class TenantContextService {
  private context: TenantContext = {};

  get tenantId(): string | undefined {
    return this.context.tenantId;
  }

  setTenantId(tenantId: string | undefined): void {
    this.context = { tenantId };
  }
}

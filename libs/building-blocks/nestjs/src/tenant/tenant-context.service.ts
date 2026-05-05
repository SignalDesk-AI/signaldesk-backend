import { Injectable } from '@nestjs/common';
import { AsyncLocalStorage } from 'node:async_hooks';

import { TenantContext } from './tenant-context';

@Injectable()
export class TenantContextService {
  private readonly storage = new AsyncLocalStorage<TenantContext>();

  get tenantId(): string | undefined {
    return this.storage.getStore()?.tenantId;
  }

  setTenantId(tenantId: string | undefined): void {
    const context = this.storage.getStore();

    if (!context) {
      throw new Error('Tenant context is not initialized for the current async flow.');
    }

    context.tenantId = tenantId;
  }

  run<T>(context: TenantContext, callback: () => T): T {
    return this.storage.run({ ...context }, callback);
  }
}

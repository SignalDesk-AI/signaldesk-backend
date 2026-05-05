import { Injectable } from '@nestjs/common';
import { AsyncLocalStorage } from 'node:async_hooks';

import { CorrelationContext } from './correlation-context';

@Injectable()
export class CorrelationContextService {
  private readonly storage = new AsyncLocalStorage<CorrelationContext>();

  get correlationId(): string | undefined {
    return this.storage.getStore()?.correlationId;
  }

  setCorrelationId(correlationId: string | undefined): void {
    const context = this.storage.getStore();

    if (!context) {
      throw new Error('Correlation context is not initialized for the current async flow.');
    }

    context.correlationId = correlationId;
  }

  run<T>(context: CorrelationContext, callback: () => T): T {
    return this.storage.run({ ...context }, callback);
  }
}

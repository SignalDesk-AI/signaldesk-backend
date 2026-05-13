import { Injectable } from '@nestjs/common';
import { AsyncLocalStorage } from 'node:async_hooks';

import { CorrelationContextService } from '../correlation';
import { TraceContext, resolveTraceContext } from './trace-context';

@Injectable()
export class TraceContextService {
  private readonly storage = new AsyncLocalStorage<TraceContext>();

  constructor(private readonly correlationContext: CorrelationContextService) {}

  get traceId(): string | undefined {
    return this.storage.getStore()?.traceId;
  }

  get correlationId(): string | undefined {
    return this.storage.getStore()?.correlationId ?? this.correlationContext.correlationId;
  }

  fromHeaders(headers: Record<string, string | string[] | undefined>): TraceContext {
    return resolveTraceContext(headers, this.correlationContext.correlationId);
  }

  run<T>(context: TraceContext, callback: () => T): T {
    return this.storage.run({ ...context }, callback);
  }
}

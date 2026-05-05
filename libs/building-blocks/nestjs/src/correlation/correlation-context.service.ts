import { Injectable } from '@nestjs/common';

import { CorrelationContext } from './correlation-context';

@Injectable()
export class CorrelationContextService {
  private context: CorrelationContext = {};

  get correlationId(): string | undefined {
    return this.context.correlationId;
  }

  setCorrelationId(correlationId: string | undefined): void {
    this.context = { correlationId };
  }
}

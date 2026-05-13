import { Injectable, NestMiddleware } from '@nestjs/common';

import { CORRELATION_ID_HEADER } from './correlation-context';
import { CorrelationContextService } from './correlation-context.service';
import { HeaderValue, resolveCorrelationId } from './correlation-id';

interface HeaderCarrier {
  headers: Record<string, HeaderValue>;
}

interface HeaderResponse {
  setHeader(name: string, value: string): void;
}

@Injectable()
export class CorrelationMiddleware implements NestMiddleware {
  constructor(private readonly context: CorrelationContextService) {}

  use(request: HeaderCarrier, response: HeaderResponse, next: () => void): void {
    const correlationId = resolveCorrelationId(request.headers);

    request.headers[CORRELATION_ID_HEADER] = correlationId;
    response.setHeader(CORRELATION_ID_HEADER, correlationId);

    this.context.run({ correlationId }, next);
  }
}

import { CorrelationContextService } from './correlation-context.service';
import { CorrelationMiddleware } from './correlation.middleware';
import { normalizeCorrelationId, resolveCorrelationId } from './correlation-id';

describe('correlation helpers', () => {
  it('normalizes blank and array header values', () => {
    expect(normalizeCorrelationId(' correlation-1 ')).toBe('correlation-1');
    expect(normalizeCorrelationId(['correlation-2', 'correlation-3'])).toBe('correlation-2');
    expect(normalizeCorrelationId('')).toBeUndefined();
  });

  it('preserves incoming correlation IDs before generating fallback IDs', () => {
    expect(resolveCorrelationId({ 'x-correlation-id': 'correlation-1' }, () => 'fallback')).toBe(
      'correlation-1'
    );
    expect(resolveCorrelationId({}, () => 'fallback')).toBe('fallback');
  });

  it('stores the resolved correlation ID for the middleware async flow', () => {
    const context = new CorrelationContextService();
    const middleware = new CorrelationMiddleware(context);
    const request = { headers: {} as Record<string, string> };
    const response = createResponse();
    let resolvedCorrelationId: string | undefined;

    middleware.use(request, response, () => {
      resolvedCorrelationId = context.correlationId;
    });

    expect(resolvedCorrelationId).toBeDefined();
    expect(request.headers['x-correlation-id']).toBe(resolvedCorrelationId);
    expect(response.headers['x-correlation-id']).toBe(resolvedCorrelationId);
  });
});

function createResponse(): { headers: Record<string, string>; setHeader: (name: string, value: string) => void } {
  return {
    headers: {},
    setHeader(name: string, value: string): void {
      this.headers[name] = value;
    },
  };
}

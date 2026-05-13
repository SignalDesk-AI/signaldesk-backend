import { CorrelationContextService } from '../correlation';
import { TraceContextService } from './trace-context.service';
import { applyTraceHeaders, resolveTraceContext } from './trace-context';

describe('trace context helpers', () => {
  it('resolves trace ID from headers and falls back to correlation ID', () => {
    expect(
      resolveTraceContext({
        'x-trace-id': 'trace-1',
        'x-correlation-id': 'correlation-1',
      })
    ).toEqual({ traceId: 'trace-1', correlationId: 'correlation-1' });

    expect(resolveTraceContext({ 'x-correlation-id': 'correlation-1' })).toEqual({
      traceId: 'correlation-1',
      correlationId: 'correlation-1',
    });
  });

  it('applies trace and correlation headers', () => {
    expect(applyTraceHeaders({}, { traceId: 'trace-1', correlationId: 'correlation-1' })).toEqual({
      'x-trace-id': 'trace-1',
      'x-correlation-id': 'correlation-1',
    });
  });

  it('stores trace context and uses current correlation fallback', () => {
    const correlation = new CorrelationContextService();
    const tracing = new TraceContextService(correlation);

    const result = correlation.run({ correlationId: 'correlation-1' }, () =>
      tracing.run(tracing.fromHeaders({}), () => ({
        traceId: tracing.traceId,
        correlationId: tracing.correlationId,
      }))
    );

    expect(result).toEqual({ traceId: 'correlation-1', correlationId: 'correlation-1' });
  });
});

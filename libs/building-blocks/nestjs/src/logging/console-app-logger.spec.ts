import { Logger } from '@nestjs/common';

import { CorrelationContextService } from '../correlation';
import { TenantContextService } from '../tenant';
import { TraceContextService } from '../tracing';
import { ConsoleAppLogger } from './console-app-logger';

describe('ConsoleAppLogger', () => {
  it('adds request context to structured log output', () => {
    const logSpy = jest.spyOn(Logger.prototype, 'log').mockImplementation();
    const correlation = new CorrelationContextService();
    const tenant = new TenantContextService();
    const tracing = new TraceContextService(correlation);
    const logger = new ConsoleAppLogger(correlation, tenant, tracing);

    correlation.run({ correlationId: 'correlation-1' }, () => {
      tenant.run({ tenantId: 'tenant-1' }, () => {
        tracing.run({ traceId: 'trace-1', correlationId: 'correlation-1' }, () => {
          logger.info('request handled', { serviceName: 'gateway-bff' });
        });
      });
    });

    expect(JSON.parse(logSpy.mock.calls[0][0] as string)).toEqual({
      message: 'request handled',
      correlationId: 'correlation-1',
      tenantId: 'tenant-1',
      traceId: 'trace-1',
      serviceName: 'gateway-bff',
    });
    logSpy.mockRestore();
  });
});

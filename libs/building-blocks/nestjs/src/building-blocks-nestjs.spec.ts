import { CorrelationContextService } from './correlation/correlation-context.service';
import { HealthService } from './health/health.service';
import { TenantContextService } from './tenant/tenant-context.service';

describe('building-blocks-nestjs', () => {
  it('returns live and ready health responses', () => {
    const health = new HealthService();

    const live = health.live('test-service');
    const ready = health.ready('test-service');

    expect(live.status).toBe('ok');
    expect(live.service).toBe('test-service');
    expect(Date.parse(live.checkedAt)).not.toBeNaN();
    expect(ready.status).toBe('ok');
    expect(ready.service).toBe('test-service');
    expect(Date.parse(ready.checkedAt)).not.toBeNaN();
  });

  it('stores tenant context', () => {
    const tenant = new TenantContextService();

    tenant.setTenantId('tenant-1');

    expect(tenant.tenantId).toBe('tenant-1');
  });

  it('stores correlation context', () => {
    const correlation = new CorrelationContextService();

    correlation.setCorrelationId('correlation-1');

    expect(correlation.correlationId).toBe('correlation-1');
  });
});

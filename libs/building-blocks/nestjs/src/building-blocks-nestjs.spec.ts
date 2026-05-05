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

    const tenantId = tenant.run({ tenantId: undefined }, () => {
      tenant.setTenantId('tenant-1');
      return tenant.tenantId;
    });

    expect(tenantId).toBe('tenant-1');
  });

  it('isolates tenant context between async flows', async () => {
    const tenant = new TenantContextService();

    const [first, second] = await Promise.all([
      tenant.run({ tenantId: 'tenant-1' }, async () => {
        await Promise.resolve();
        return tenant.tenantId;
      }),
      tenant.run({ tenantId: 'tenant-2' }, async () => {
        await Promise.resolve();
        return tenant.tenantId;
      }),
    ]);

    expect(first).toBe('tenant-1');
    expect(second).toBe('tenant-2');
  });

  it('stores correlation context', () => {
    const correlation = new CorrelationContextService();

    const correlationId = correlation.run({ correlationId: undefined }, () => {
      correlation.setCorrelationId('correlation-1');
      return correlation.correlationId;
    });

    expect(correlationId).toBe('correlation-1');
  });

  it('isolates correlation context between async flows', async () => {
    const correlation = new CorrelationContextService();

    const [first, second] = await Promise.all([
      correlation.run({ correlationId: 'correlation-1' }, async () => {
        await Promise.resolve();
        return correlation.correlationId;
      }),
      correlation.run({ correlationId: 'correlation-2' }, async () => {
        await Promise.resolve();
        return correlation.correlationId;
      }),
    ]);

    expect(first).toBe('correlation-1');
    expect(second).toBe('correlation-2');
  });
});

import { METHOD_METADATA, PATH_METADATA } from '@nestjs/common/constants';

import { AggregateHealthController } from './aggregate-health.controller';
import { HealthController } from './health.controller';

type ReadyResponse = Parameters<HealthController['ready']>[0];

describe('HealthController', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterAll(() => {
    process.env = originalEnv;
  });

  it('preserves live health shape', () => {
    const controller = new HealthController();
    const response = controller.live();

    expect(response.status).toBe('ok');
    expect(response.service).toBe('gateway-bff');
    expect(Date.parse(response.checkedAt)).not.toBeNaN();
  });

  it('maps live and ready under /health and aggregate under /api/health with the global prefix', () => {
    expect(Reflect.getMetadata(PATH_METADATA, HealthController)).toBe('health');
    expect(Reflect.getMetadata(PATH_METADATA, HealthController.prototype.live)).toBe('live');
    expect(Reflect.getMetadata(PATH_METADATA, HealthController.prototype.ready)).toBe('ready');
    expect(Reflect.getMetadata(PATH_METADATA, AggregateHealthController)).toBe('health');
    expect(Reflect.getMetadata(PATH_METADATA, AggregateHealthController.prototype.aggregate)).toBe('/');
    expect(Reflect.getMetadata(METHOD_METADATA, AggregateHealthController.prototype.aggregate)).toBe(0);
  });

  it('returns degraded readiness payload directly with status 503', async () => {
    process.env.REDIS_URL = 'redis://127.0.0.1:1';
    const controller = new HealthController();
    const response = createResponse();

    const payload = await controller.ready(response);

    expect(response.statusCode).toBe(503);
    expect(payload.status).toBe('degraded');
    expect(payload.service).toBe('gateway-bff');
    expect(payload.dependencies).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ name: 'redis', status: 'fail' }),
      ])
    );
    expect(payload).not.toHaveProperty('error');
  });

  it('returns an aggregate placeholder without checking downstream services', () => {
    const controller = new AggregateHealthController();
    const response = controller.aggregate();

    expect(response.status).toBe('ok');
    expect(response.service).toBe('gateway-bff');
    expect(response.downstream).toEqual([
      { service: 'identity-service', status: 'not_checked', target: 'http://localhost:5001' },
      { service: 'workspace-service', status: 'not_checked', target: 'http://localhost:5002' },
      { service: 'support-service', status: 'not_checked', target: 'http://localhost:5003' },
      { service: 'knowledge-service', status: 'not_checked', target: 'http://localhost:5004' },
      { service: 'notification-service', status: 'not_checked', target: 'http://localhost:3001' },
      { service: 'search-service', status: 'not_checked', target: 'http://localhost:3002' },
      { service: 'ai-service', status: 'not_checked', target: 'http://localhost:3003' },
      { service: 'campaign-service', status: 'not_checked', target: 'http://localhost:5005' },
    ]);
  });
});

function createResponse(): ReadyResponse & { statusCode?: number } {
  return {
    status(statusCode: number) {
      this.statusCode = statusCode;
      return this;
    },
  } as ReadyResponse & { statusCode?: number };
}

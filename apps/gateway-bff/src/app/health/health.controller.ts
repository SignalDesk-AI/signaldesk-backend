import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import {
  configuredCheck,
  httpCheck,
  readinessPayload,
  tcpCheckFromUrl,
} from '@signaldesk/building-blocks-nestjs';

const serviceName = 'gateway-bff';
const downstreamUrls = [
  ['identity-service', 'IDENTITY_SERVICE_URL', 'http://localhost:5001'],
  ['workspace-service', 'WORKSPACE_SERVICE_URL', 'http://localhost:5002'],
  ['support-service', 'SUPPORT_SERVICE_URL', 'http://localhost:5003'],
  ['knowledge-service', 'KNOWLEDGE_SERVICE_URL', 'http://localhost:5004'],
  ['notification-service', 'NOTIFICATION_SERVICE_URL', 'http://localhost:3001'],
  ['search-service', 'SEARCH_SERVICE_URL', 'http://localhost:3002'],
  ['ai-service', 'AI_SERVICE_URL', 'http://localhost:3003'],
] as const;

@Controller('health')
export class HealthController {
  @Get('live')
  live() {
    return {
      status: 'ok',
      service: serviceName,
      checkedAt: new Date().toISOString(),
    };
  }

  @Get('ready')
  async ready() {
    const strictDownstreamChecks =
      process.env.GATEWAY_READY_CHECK_DOWNSTREAMS === 'true';
    const configuredDownstreams = downstreamUrls.map(([service, envName, fallback]) =>
      configuredCheck(service, process.env[envName] ?? fallback, envName)
    );

    const dependencies = [
      await tcpCheckFromUrl('redis', process.env.REDIS_URL, 'redis://localhost:6379', 6379),
      ...configuredDownstreams,
    ];

    if (strictDownstreamChecks) {
      dependencies.push(
        ...(await Promise.all(
          downstreamUrls.map(([service, envName, fallback]) =>
            httpCheck(
              `${service}-ready`,
              `${process.env[envName] ?? fallback}/health/ready`
            )
          )
        ))
      );
    }

    const payload = readinessPayload(serviceName, dependencies);

    if (payload.status !== 'ok') {
      throw new ServiceUnavailableException(payload);
    }

    return payload;
  }
}

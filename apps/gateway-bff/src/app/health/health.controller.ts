import { Controller, Get, HttpStatus, Res } from '@nestjs/common';
import {
  configuredCheck,
  httpCheck,
  readinessPayload,
  ReadinessResponse,
  tcpCheckFromUrl,
} from '@signaldesk/building-blocks-nestjs';
import { Response } from 'express';

import { downstreamUrls, serviceName } from './aggregate-health';

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
  async ready(@Res({ passthrough: true }) response: Response): Promise<ReadinessResponse> {
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
      response.status(HttpStatus.SERVICE_UNAVAILABLE);
    }

    return payload;
  }
}

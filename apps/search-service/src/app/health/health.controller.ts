import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import {
  httpCheck,
  readinessPayload,
  tcpCheckFromUrl,
} from '@signaldesk/building-blocks-nestjs';

const serviceName = 'search-service';

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
    const elasticsearchUrl =
      process.env.ELASTICSEARCH_URL ?? 'http://localhost:9200';
    const dependencies = await Promise.all([
      tcpCheckFromUrl('redis', process.env.REDIS_URL, 'redis://localhost:6379', 6379),
      tcpCheckFromUrl(
        'rabbitmq',
        process.env.RABBITMQ_URL,
        'amqp://signaldesk:signaldesk@localhost:5672',
        5672
      ),
      httpCheck('elasticsearch', elasticsearchUrl),
    ]);
    const payload = readinessPayload(serviceName, dependencies);

    if (payload.status !== 'ok') {
      throw new ServiceUnavailableException(payload);
    }

    return payload;
  }
}

import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import {
  httpCheck,
  readinessPayload,
  tcpCheckFromUrl,
} from '@signaldesk/building-blocks-nestjs';

const serviceName = 'ai-service';

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
    const ollamaUrl = process.env.OLLAMA_URL ?? 'http://localhost:11434';
    const dependencies = await Promise.all([
      tcpCheckFromUrl('redis', process.env.REDIS_URL, 'redis://localhost:6379', 6379),
      tcpCheckFromUrl(
        'rabbitmq',
        process.env.RABBITMQ_URL,
        'amqp://signaldesk:signaldesk@localhost:5672',
        5672
      ),
      tcpCheckFromUrl(
        'mongodb',
        process.env.MONGODB_URI,
        'mongodb://localhost:27017/signaldesk',
        27017
      ),
      httpCheck('ollama', `${ollamaUrl}/api/tags`),
    ]);
    const payload = readinessPayload(serviceName, dependencies);

    if (payload.status !== 'ok') {
      throw new ServiceUnavailableException(payload);
    }

    return payload;
  }
}

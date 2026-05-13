import { Injectable } from '@nestjs/common';

import { IntegrationEvent } from './integration-event';
import { RabbitMqClient } from './rabbitmq-client';

@Injectable()
export class NoopRabbitMqClient implements RabbitMqClient {
  readonly published: Array<{
    exchange: string;
    routingKey: string;
    event: IntegrationEvent;
  }> = [];

  async publish<TPayload>(
    exchange: string,
    routingKey: string,
    event: IntegrationEvent<TPayload>
  ): Promise<void> {
    this.published.push({ exchange, routingKey, event });
  }
}

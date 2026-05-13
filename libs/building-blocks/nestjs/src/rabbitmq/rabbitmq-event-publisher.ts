import { Inject, Injectable } from '@nestjs/common';

import { IntegrationEvent } from './integration-event';
import { RABBITMQ_CLIENT, RabbitMqClient, RabbitMqPublishOptions, RabbitMqPublisher } from './rabbitmq-client';

@Injectable()
export class RabbitMqEventPublisher implements RabbitMqPublisher {
  constructor(
    @Inject(RABBITMQ_CLIENT) private readonly client: RabbitMqClient
  ) {}

  publish<TPayload>(event: IntegrationEvent<TPayload>, options: RabbitMqPublishOptions): Promise<void> {
    return this.client.publish(options.exchange, options.routingKey, event);
  }
}

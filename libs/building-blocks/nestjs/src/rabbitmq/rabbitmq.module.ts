import { Module } from '@nestjs/common';

import { NoopRabbitMqClient } from './noop-rabbitmq-client';
import { RABBITMQ_CLIENT, RABBITMQ_PUBLISHER } from './rabbitmq-client';
import { RabbitMqEventPublisher } from './rabbitmq-event-publisher';

@Module({
  providers: [
    NoopRabbitMqClient,
    RabbitMqEventPublisher,
    {
      provide: RABBITMQ_CLIENT,
      useExisting: NoopRabbitMqClient,
    },
    {
      provide: RABBITMQ_PUBLISHER,
      useExisting: RabbitMqEventPublisher,
    },
  ],
  exports: [RABBITMQ_CLIENT, RABBITMQ_PUBLISHER, NoopRabbitMqClient, RabbitMqEventPublisher],
})
export class RabbitMqModule {}

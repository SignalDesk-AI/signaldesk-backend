import { IntegrationEvent } from './integration-event';

export interface RabbitMqClient {
  publish<TPayload>(
    exchange: string,
    routingKey: string,
    event: IntegrationEvent<TPayload>
  ): Promise<void>;
}

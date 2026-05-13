import { IntegrationEvent } from './integration-event';

export const RABBITMQ_CLIENT = Symbol('RABBITMQ_CLIENT');
export const RABBITMQ_PUBLISHER = Symbol('RABBITMQ_PUBLISHER');

export interface RabbitMqPublishOptions {
  exchange: string;
  routingKey: string;
}

export interface RabbitMqClient {
  publish<TPayload>(
    exchange: string,
    routingKey: string,
    event: IntegrationEvent<TPayload>
  ): Promise<void>;
}

export interface RabbitMqPublisher {
  publish<TPayload>(event: IntegrationEvent<TPayload>, options: RabbitMqPublishOptions): Promise<void>;
}

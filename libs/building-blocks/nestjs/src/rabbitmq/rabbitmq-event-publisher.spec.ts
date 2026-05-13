import { NoopRabbitMqClient } from './noop-rabbitmq-client';
import { RabbitMqEventPublisher } from './rabbitmq-event-publisher';

describe('RabbitMqEventPublisher', () => {
  it('forwards events to the configured RabbitMQ client abstraction', async () => {
    const client = new NoopRabbitMqClient();
    const publisher = new RabbitMqEventPublisher(client);
    const event = {
      eventId: 'event-1',
      eventType: 'support.ticket.created',
      occurredAt: new Date(0).toISOString(),
      payload: { ticketId: 'ticket-1' },
    };

    await publisher.publish(event, {
      exchange: 'support.events',
      routingKey: 'support.ticket.created',
    });

    expect(client.published).toEqual([
      {
        exchange: 'support.events',
        routingKey: 'support.ticket.created',
        event,
      },
    ]);
  });
});

# RabbitMQ Topology And DLQ Policy

Day 2 locks the topology and the consumer retry contract. RabbitMQ provides the
workload, retry, replay, and final DLQ routes; consumers decide when a message has
become permanent failure.

## Exchanges

- `signaldesk.domain-events`: durable topic exchange for domain events and replay routes.
- `signaldesk.domain-events.dlx`: durable topic exchange for retry and final DLQ routes.

## Workload Queues

| Workload | Main queue | Retry queue | Final DLQ |
| --- | --- | --- | --- |
| Notification | `notification.events` | `notification.events.retry` | `notification.events.dlq` |
| Search indexing | `search.indexing` | `search.indexing.retry` | `search.indexing.dlq` |
| AI tasks | `ai.tasks` | `ai.tasks.retry` | `ai.tasks.dlq` |
| Campaign | `campaign.events` | `campaign.events.retry` | `campaign.events.dlq` |

## Consumer Contract

Consumers must use this flow for retryable failures:

1. Read `x-signaldesk-retry-count` from the incoming message headers. Missing value means `0`.
2. If the failure is transient and retry count is lower than `RABBITMQ_MAX_RETRY_COUNT`,
   republish the message to `signaldesk.domain-events.dlx` with the workload retry
   routing key and incremented `x-signaldesk-retry-count`.
3. The retry queue holds the message for its TTL, then dead-letters it back to
   `signaldesk.domain-events` using the workload replay routing key.
4. The replay routing key is bound back to the original workload queue.
5. If retry count reaches `RABBITMQ_MAX_RETRY_COUNT`, publish to
   `signaldesk.domain-events.dlx` with the workload final DLQ routing key.
6. .NET consumers that have PostgreSQL also insert a row in `ops.dead_letters`.

Permanent or poison-message failures should skip retry and go directly to final DLQ.

## Local Defaults

- `RABBITMQ_MAX_RETRY_COUNT=10`
- `RABBITMQ_RETRY_DELAY_MS=30000`
- `ai.tasks.retry` uses a longer TTL of `60000` because model/provider failures tend
  to recover more slowly.

## Manual Replay Rule

Manual replay republishes the original payload to the workload replay routing key.
It is safe only because every consumer must be idempotent through Redis, MongoDB, or
`ops.inbox_messages` depending on the service owner.

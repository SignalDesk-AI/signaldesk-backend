# Local Runtime Rules

This file locks the Day 1 local runtime conventions for backend services and infrastructure.

## Docker

- Compose file: `infra/docker/docker-compose.local.yml`
- Docker project name: `signaldesk-local`
- Docker network: `signaldesk-local`
- Container DNS names must use the Compose service name, for example `postgres`, `pgbouncer`, `redis`, `rabbitmq`, `mongodb`, `elasticsearch`, `minio`, `mailpit`, and `ollama`.
- Services running inside Docker should not call infrastructure by `localhost`; they should use the DNS names above.
- Services running directly on the host can use the published localhost ports from `.env.example`.

## Port Rules

| Component | Host Port |
| --- | ---: |
| gateway-bff | 3000 |
| notification-service | 3001 |
| search-service | 3002 |
| ai-service | 3003 |
| identity-service | 5001 |
| workspace-service | 5002 |
| support-service | 5003 |
| knowledge-service | 5004 |
| campaign-service | 5005 |
| PostgreSQL | 5432 |
| PgBouncer | 6432 |
| MongoDB | 27017 |
| Redis | 6379 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ management | 15672 |
| Elasticsearch | 9200 |
| MinIO API | 9000 |
| MinIO console | 9001 |
| Mailpit SMTP | 1025 |
| Mailpit UI | 8025 |
| Ollama | 11434 |

## Environment Naming

- Service ports use `<SERVICE_NAME>_PORT`, for example `IDENTITY_SERVICE_PORT`.
- Infrastructure URLs use stable names: `MONGODB_URI`, `REDIS_URL`, `RABBITMQ_URL`, `ELASTICSEARCH_URL`, `MINIO_ENDPOINT`.
- PostgreSQL settings use `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`.
- PgBouncer settings use `PGBOUNCER_HOST` and `PGBOUNCER_PORT`.
- JWT key paths use `JWT_PUBLIC_KEY_PATH` and `JWT_PRIVATE_KEY_PATH`.

## Local Commands

Validate the Compose file:

```powershell
docker compose -f infra/docker/docker-compose.local.yml config
```

Start local infrastructure:

```powershell
docker compose -f infra/docker/docker-compose.local.yml up -d
```

Check local infrastructure:

```powershell
docker compose -f infra/docker/docker-compose.local.yml ps
```

Stop local infrastructure:

```powershell
docker compose -f infra/docker/docker-compose.local.yml down
```

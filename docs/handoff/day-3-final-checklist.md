# Day 3 Final Checklist

Day 3 scope stayed focused on shared building blocks, observability foundations, gateway request/proxy/health skeletons, and minimal .NET service health/context conventions.

## Implementation Checklist

- [x] NestJS building blocks
  - Error envelope helper/filter matches `docs/api/error-envelope.md` with top-level `error` and no top-level `data`.
  - Correlation middleware preserves or generates `X-Correlation-Id`.
  - Tenant context and tenant mismatch helpers are exported.
  - In-memory token bucket abstractions are available without Redis dependencies.
  - Lightweight trace context helpers use `X-Trace-Id` plus correlation ID and are exported through `TracingModule`.
  - Structured logging exports `APP_LOGGER` / `ConsoleAppLogger` with correlation, tenant, and trace context for apps to consume; gateway request logging middleware is not wired yet.
  - Redis cache and RabbitMQ publisher/client abstractions are interface-ready with local in-memory/no-op behavior only.
- [x] Gateway request pipeline
  - Public routes include `GET /health/live`, `GET /health/ready`, `GET /api/health`, and placeholder auth register/login/refresh routes.
  - Protected routes use the placeholder JWT verifier and derive trusted tenant context from token claims.
  - Client-provided tenant IDs are mismatch-checked instead of trusted.
  - Global gateway rate limiting is wired after auth; protected routes use trusted token tenant IDs, while public routes use route-group plus IP/header fallback keys.
  - Rate-limit failures return HTTP 429 using the documented `RATE_LIMITED` error envelope.
- [x] Gateway proxy and health
  - Route map covers `/api/auth`, `/api/users`, `/api/workspace`, `/api/support`, `/api/knowledge`, `/api/notifications`, `/api/search`, `/api/ai`, and `/api/campaigns`.
  - Proxy strips the gateway-owned `/api` prefix before forwarding route-map-owned paths and forwards `Authorization`, `X-Correlation-Id`, trusted `X-Tenant-Id`, and `Content-Type`.
  - `GET /api/health` returns an aggregate placeholder with downstream targets listed as `not_checked`.
  - `GET /health/ready` checks Redis by TCP, verifies downstream URL configuration, can optionally call downstream readiness endpoints with `GATEWAY_READY_CHECK_DOWNSTREAMS=true`, and preserves the readiness payload body when returning HTTP 503.
- [x] .NET building blocks
  - Tenant, correlation, current-user, unit-of-work, validation, audit, Redis, health, and Outbox abstractions are present.
  - Outbox status mapping uses explicit storage values: `pending`, `processing`, `published`, and `dead_letter`.
- [x] .NET service skeleton health/context conventions
  - Identity, workspace, support, knowledge, and campaign APIs preserve `GET /health/live` and `GET /health/ready`.
  - APIs use shared `HealthResponse` / `DependencyHealth` and minimal request/trace context registration.

## Verification Results

Latest Day 3 review-fix run:

| Command | Result | Notes |
| --- | --- | --- |
| `npm run build:node -- --skip-nx-cache` | Passed | Nx successfully built 5 Node projects; Node emitted `[DEP0180] fs.Stats constructor is deprecated`. |
| `npm run test:node -- --skip-nx-cache` | Passed | Nx successfully tested 5 Node projects; 27 building-block tests and 36 gateway tests passed, plus service skeleton tests. |
| `powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1` | Passed | Building blocks plus campaign, identity, knowledge, support, and workspace APIs built with 0 warnings and 0 errors. |
| `docker compose -f infra/docker/docker-compose.local.yml config` | Passed | Compose configuration rendered successfully. |

Commands to rerun after Day 3 implementation changes:

```powershell
npm run build:node -- --skip-nx-cache
npm run test:node -- --skip-nx-cache
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

## Known Gaps Before Day 4

- JWT verification is still a placeholder and must be replaced with real identity keys/claims validation.
- Gateway proxying is mechanical only; downstream business APIs are not implemented.
- Gateway aggregate health does not deeply check downstream services unless strict readiness checks are enabled, and `/api/health` remains a placeholder.
- NestJS tracing/logging remains a lightweight context foundation; gateway request logging middleware and full OpenTelemetry/Winston integration are deferred and no dependencies were added.
- NestJS Redis and RabbitMQ helpers are interface-ready only and do not connect to real Redis/RabbitMQ.
- Gateway rate limiting uses an in-memory bucket for Day 3 local skeleton behavior; distributed Redis-backed limits are still needed before multi-instance deployment.
- .NET readiness checks currently validate PgBouncer TCP reachability only.
- Redis abstractions remain no-op/interface-ready on the .NET side.
- No RabbitMQ publishing, business handlers, migrations, or product endpoints were added during Day 3.

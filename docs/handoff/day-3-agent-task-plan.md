# Day 3 Agent Task Plan

This plan splits Day 3 into small, safe implementation tasks for separate CLI agents. Each task should be assigned independently. Agents must read `AGENTS.md` first and keep work scoped to the listed files.

Day 3 goal: building blocks, observability foundation, and Gateway V1 skeleton. Do not implement Day 4+ business logic.

Global guardrails:

- Do not edit `.env`, secrets, or local credentials.
- Do not create branches or worktrees unless the user explicitly asks.
- Do not stage or commit unless the user explicitly asks.
- Do not run Codex CLI, Claude Code, opencode, or other agent CLIs from inside a task unless the user explicitly asks for nested orchestration.
- Do not introduce Kafka in the core phase.
- Preserve existing `/health/live` and `/health/ready` endpoints.
- Keep client-facing error responses aligned with `docs/api/error-envelope.md`.
- Tenant isolation is mandatory. Reject tenant mismatch rather than trusting client-provided tenant IDs.
- .NET domain events must use an Outbox abstraction. Do not publish RabbitMQ directly from request handlers.
- Async consumers must be idempotent.

Recommended execution order:

1. Task 1 and Task 4 can run in parallel because they touch different shared libraries.
2. Task 2 depends on Task 1.
3. Task 3 can start after Task 1 and may overlap with Task 2 if write paths stay separate.
4. Task 5 depends on Task 4.
5. Task 6 runs last.

## Task 1: NestJS Common Building Blocks

Agent type: Codex CLI

Scope:

- Strengthen shared NestJS foundation under `libs/building-blocks/nestjs/src`.
- Add or refine helpers for error envelopes, correlation context, tenant context/mismatch handling, minimal token bucket abstraction with in-memory fallback, and structured logging helpers if practical.
- No gateway proxy implementation in this task.

Allowed files/folders:

- `libs/building-blocks/nestjs/src/**`
- `libs/building-blocks/nestjs/project.json`
- `libs/building-blocks/nestjs/tsconfig*.json`
- `libs/building-blocks/nestjs/jest.config.cts`
- `libs/building-blocks/nestjs/README.md`

Forbidden files/folders:

- `.env`
- `apps/**`
- `infra/**`
- `libs/building-blocks/dotnet/**`
- Business service code

Implementation requirements:

- Export new helpers from `libs/building-blocks/nestjs/src/index.ts`.
- Implement an error envelope helper/filter compatible with `docs/api/error-envelope.md`.
- Provide correlation ID helper/middleware or interceptor pattern that can generate or preserve `X-Correlation-Id`.
- Provide tenant context helper and tenant mismatch error helper for gateway use.
- Provide a minimal token bucket abstraction with an in-memory fallback. Redis integration can remain interface-ready if Redis client dependency is not present.
- Keep dependencies minimal and avoid adding new packages unless clearly necessary.
- Add focused unit tests for pure helpers.

Acceptance criteria:

- Existing building-block exports still compile.
- New helpers are small, documented by names/tests, and do not pull in app-specific code.
- Error envelope uses top-level `error` without top-level `data`.
- Tenant mismatch maps to a 403-style code/path suitable for `FORBIDDEN`.
- Token bucket can be used without Redis for local skeleton behavior.

Test command:

```powershell
npm run build:node
npm run test:node
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 1 in the SignalDesk backend repo.

Read AGENTS.md, docs/api/error-envelope.md, package.json, nx.json, and the current files under libs/building-blocks/nestjs/src.

Implement only NestJS common building blocks under libs/building-blocks/nestjs/src:
- error envelope helper/filter compatible with docs/api/error-envelope.md
- correlation ID helper/middleware or interceptor pattern
- tenant context/mismatch helper
- minimal token bucket abstraction with in-memory fallback
- structured logging helper only if it fits existing patterns

Do not touch apps/**, infra/**, .env, secrets, or business logic. Do not implement gateway proxying.

Run npm run build:node and npm run test:node if possible. Final response must list changed files, verification commands, and remaining risks.
```

## Task 2: Gateway V1 Request Pipeline

Agent type: Claude Code

Scope:

- Implement Gateway V1 request pipeline in `apps/gateway-bff/src/app`.
- Add correlation middleware, auth guard/JWT placeholder or public-key-ready verifier, tenant resolution from token, tenant mismatch rejection, and public/protected route rules.
- No Day 4 auth business logic.

Allowed files/folders:

- `apps/gateway-bff/src/app/**`
- `apps/gateway-bff/src/main.ts`
- `apps/gateway-bff/project.json`
- `apps/gateway-bff/tsconfig*.json`
- `apps/gateway-bff/jest.config.cts`
- Read-only reference to `libs/building-blocks/nestjs/src/**`

Forbidden files/folders:

- `.env`
- `apps/identity-service/**`
- Other business services except read-only route/health reference
- `infra/**`
- `libs/building-blocks/dotnet/**`

Implementation requirements:

- Use shared helpers from Task 1 when available.
- Generate or preserve `X-Correlation-Id` on every request.
- Resolve tenant from verified or decoded token claims in a placeholder-safe way.
- Reject mismatch between trusted token tenant and client-provided `X-Tenant-Id`, route params, query, or body where checked.
- Define clear public route rules for health and auth entrypoints.
- Define protected route rules for downstream API prefixes.
- JWT verifier may be placeholder/public-key-ready, but must fail closed for protected routes when token is missing or invalid.
- Do not implement registration/login/refresh business behavior.

Acceptance criteria:

- `/health/live` and `/health/ready` remain public.
- Auth routes needed for login/register can be configured public for proxying.
- Protected routes require auth context.
- Mismatch returns the documented error envelope shape and `FORBIDDEN`.
- Correlation ID is available to downstream proxy task.

Test command:

```powershell
npm run build:node
npm run test:node
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 2 in the SignalDesk backend repo.

Read AGENTS.md, docs/api/error-envelope.md, docs/handoff/gateway-route-map.md, apps/gateway-bff/src/app, and libs/building-blocks/nestjs/src.

Implement only the Gateway V1 request pipeline in apps/gateway-bff:
- correlation middleware
- auth guard or JWT placeholder/public-key-ready verifier
- tenant resolution from token
- reject tenant mismatch
- public/protected route rules

Do not implement Day 4 auth business logic. Do not modify identity-service, .env, secrets, or infra. Do not implement proxy route map unless it is required as a tiny hook for request context.

Run npm run build:node and npm run test:node if possible. Final response must list changed files, verification commands, and remaining risks.
```

## Task 3: Gateway Proxy Route Map + Health

Agent type: Codex CLI

Scope:

- Implement route-map based Gateway proxy skeleton and health aggregate placeholder.
- Scope is limited to `apps/gateway-bff/src/app/proxy` and `apps/gateway-bff/src/app/health`.
- Preserve existing live/ready endpoints.

Allowed files/folders:

- `apps/gateway-bff/src/app/proxy/**`
- `apps/gateway-bff/src/app/health/**`
- `apps/gateway-bff/src/app/app.module.ts` only for module wiring
- Read-only reference to `docs/handoff/gateway-route-map.md`
- Read-only reference to `libs/building-blocks/nestjs/src/**`

Forbidden files/folders:

- `.env`
- Business service implementations
- `apps/gateway-bff/src/app/auth/**` unless coordinating with Task 2
- `infra/**`
- `libs/building-blocks/dotnet/**`

Implementation requirements:

- Create a route map for:
  - `/api/auth` -> `identity-service`
  - `/api/users` -> `identity-service`
  - `/api/workspace` -> `workspace-service`
  - `/api/support` -> `support-service`
  - `/api/knowledge` -> `knowledge-service`
  - `/api/notifications` -> `notification-service`
  - `/api/search` -> `search-service`
  - `/api/ai` -> `ai-service`
  - `/api/campaigns` -> `campaign-service`
- Preserve `/health/live` and `/health/ready`.
- Add `/api/health` aggregate placeholder with downstream service names/status shape.
- Forward `X-Correlation-Id` and trusted `X-Tenant-Id` to downstream services.
- Keep proxy behavior mechanical. Do not add business decisions in gateway.
- Use environment variable names consistent with current health controller fallbacks where possible.

Acceptance criteria:

- Route map matches `docs/handoff/gateway-route-map.md`.
- Health endpoints remain stable.
- `/api/health` returns a clear aggregate placeholder without requiring all downstream services to exist.
- Forwarding headers are wired or clearly prepared for Task 2 request context.
- No business service files changed.

Test command:

```powershell
npm run build:node
npm run test:node
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 3 in the SignalDesk backend repo.

Read AGENTS.md, docs/handoff/gateway-route-map.md, docs/api/error-envelope.md, apps/gateway-bff/src/app/proxy, and apps/gateway-bff/src/app/health.

Implement only the gateway proxy route map and health aggregate placeholder:
- route map to identity/workspace/support/knowledge/notification/search/ai/campaign
- preserve /health/live and /health/ready
- add /api/health aggregate placeholder
- forward X-Correlation-Id and trusted X-Tenant-Id

Do not implement business logic. Avoid touching auth unless absolutely required for type/context integration with Task 2. Do not edit .env or infra.

Run npm run build:node and npm run test:node if possible. Final response must list changed files, verification commands, and remaining risks.
Use the existing auth/request-context pipeline from Task 2. Keep /api/health public via existing public-routes; do not edit auth unless absolutely required.

```

## Task 4: .NET Building Blocks

Agent type: opencode

Scope:

- Build shared .NET abstractions under `libs/building-blocks/dotnet`.
- No concrete business service implementation unless needed for compile.

Allowed files/folders:

- `libs/building-blocks/dotnet/**`
- `libs/building-blocks/dotnet/BuildingBlocks.sln`
- Read-only reference to `infra/postgres/init/002-ops-baseline.sql`

Forbidden files/folders:

- `.env`
- `apps/**` except read-only compile reference
- `infra/**` except read-only reference
- `libs/building-blocks/nestjs/**`
- Business endpoints or service-specific domain logic

Implementation requirements:

- Add or refine abstractions for:
  - tenant context
  - correlation context
  - current user context
  - unit of work
  - validation pipeline
  - outbox aligned with `ops.outbox_events`
  - audit logging
  - Redis cache
  - Redis distributed lock
  - Redis idempotency
  - health response convention helpers
- Keep interfaces small and implementation-light.
- Do not publish directly to RabbitMQ from request handlers.
- Ensure naming aligns with existing projects such as `BuildingBlocks.Application`, `BuildingBlocks.Domain`, `BuildingBlocks.Infrastructure`, and `BuildingBlocks.Messaging`.

Acceptance criteria:

- Building block projects compile.
- Abstractions are reusable across identity/workspace/support/knowledge/campaign.
- Outbox contract is compatible with the existing ops baseline table intent.
- Redis abstractions do not require a concrete Redis package unless already present or clearly justified.
- No business services are implemented.

Test command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 4 in the SignalDesk backend repo.

Read AGENTS.md, DOC/SIGNALDESK_AI_BE_v6_0.md sections on Outbox/tenant isolation, infra/postgres/init/002-ops-baseline.sql, and libs/building-blocks/dotnet.

Implement only shared .NET building block abstractions under libs/building-blocks/dotnet:
- tenant context/correlation/current user abstractions
- unit of work abstraction
- validation pipeline abstraction
- outbox abstraction aligned with ops.outbox_events
- audit abstraction
- Redis abstractions for cache/distributed lock/idempotency
- health response convention helpers

Do not implement business service endpoints. Do not edit apps/** unless absolutely required to keep references compiling, and report that explicitly. Do not edit .env or secrets.

Run powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1 if possible. Final response must list changed files, verification commands, and remaining risks.
```

## Task 5: Apply Minimal Health/Context Conventions To Service Skeletons

Agent type: Claude Code

Scope:

- Apply shared .NET health/context conventions to service API skeletons where feasible.
- Keep all existing health endpoints working.
- Do not implement business endpoints.

Allowed files/folders:

- `apps/identity-service/src/SignalDesk.Identity.API/Program.cs`
- `apps/workspace-service/src/SignalDesk.Workspace.API/Program.cs`
- `apps/support-service/src/SignalDesk.Support.API/Program.cs`
- `apps/knowledge-service/src/SignalDesk.Knowledge.API/Program.cs`
- `apps/campaign-service/src/SignalDesk.Campaign.API/Program.cs`
- Service API `.csproj` files only if needed for shared building block references
- Read-only reference to `libs/building-blocks/dotnet/**`

Forbidden files/folders:

- `.env`
- Business service Domain/Application/Infrastructure code except project references if necessary
- NestJS apps
- `infra/**`

Implementation requirements:

- Preserve `/health/live` and `/health/ready`.
- Use shared health response helpers from Task 4 if available and low-risk.
- Add minimal correlation/tenant/current-user context wiring only where it is convention-level and does not implement business behavior.
- Keep endpoints skeleton-only.
- Avoid broad Program.cs rewrites if small changes work.

Acceptance criteria:

- All .NET API skeletons still expose live/ready health endpoints.
- Health response shape is consistent enough for gateway aggregate health.
- Build succeeds or blockers are clearly reported.
- No business endpoints or domain logic added.

Test command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 5 in the SignalDesk backend repo.

Read AGENTS.md, apps/*-service/src/*API/Program.cs, and libs/building-blocks/dotnet.

Apply minimal shared health/context conventions to the .NET service skeleton APIs:
- identity
- workspace
- support
- knowledge
- campaign

Preserve /health/live and /health/ready. Do not implement business endpoints, command handlers, migrations, or RabbitMQ publishing. Use shared helpers only where feasible and low-risk.

Run powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1 if possible. Final response must list changed files, verification commands, and remaining risks.
```

## Task 6: Docs + Verification Cleanup

Agent type: Codex CLI

Scope:

- Clean up handoff docs after implementation tasks.
- Run verification commands and create final Day 3 checklist.
- This task should not implement product code.

Allowed files/folders:

- `docs/handoff/gateway-route-map.md`
- `docs/api/error-envelope.md` only if needed to clarify actual envelope behavior
- `README.md` only if needed
- `docs/handoff/day-3-agent-task-plan.md`
- New docs under `docs/handoff/**` if needed

Forbidden files/folders:

- `.env`
- `apps/**` implementation files
- `libs/**` implementation files
- `infra/**` except read-only verification
- Secrets or credentials

Implementation requirements:

- Update docs to match what Tasks 1-5 actually implemented.
- Keep `gateway-route-map.md` accurate for gateway prefixes and downstream ownership.
- Keep `error-envelope.md` as the contract source if envelope helpers changed details.
- Add a final Day 3 checklist covering:
  - NestJS building blocks
  - Gateway request pipeline
  - Gateway proxy and health
  - .NET building blocks
  - .NET service skeleton health/context conventions
  - verification results
  - known gaps before Day 4
- Do not paper over failed verification. Record exact failures and likely owner.

Acceptance criteria:

- Handoff docs are accurate and concise.
- Verification commands are run or explicitly marked blocked.
- Remaining risks are clear enough for the Day 4 agent.
- No code implementation files changed by this task unless the user explicitly asks.

Test command:

```powershell
npm run build:node
npm run test:node
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

Prompt ready-to-copy:

```text
You are implementing Day 3 Task 6 in the SignalDesk backend repo.

Read AGENTS.md, docs/handoff/day-3-agent-task-plan.md, docs/handoff/gateway-route-map.md, docs/api/error-envelope.md, README.md, and the changed files from Tasks 1-5.

Do docs and verification cleanup only:
- update gateway-route-map.md if the implemented route map differs
- update error-envelope.md only if the helper contract needs clarification
- update README only if Day 3 startup/verification notes are missing
- create or update a final Day 3 checklist
- run verification commands and record results

Do not implement product code. Do not edit .env, secrets, apps/** implementation files, or libs/** implementation files unless the user explicitly asks.

Run:
- npm run build:node
- npm run test:node
- powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
- docker compose -f infra/docker/docker-compose.local.yml config

Final response must list changed files, verification commands, and remaining risks.
```

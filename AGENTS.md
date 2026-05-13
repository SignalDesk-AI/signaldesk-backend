# SignalDesk Backend Agent Instructions

## Project Overview

SignalDesk AI Backend is a multi-tenant customer support platform for SaaS/SME workflows. The backend is a mixed .NET and NestJS monorepo with a gateway BFF, core business services, async event processing, search, notifications, AI/RAG, and campaign automation.

Current repo layout:

- `apps/`: deployable services.
- `libs/`: shared contracts and building blocks.
- `infra/`: Docker, database, broker, proxy, and observability files.
- `docs/`: architecture, API, and handoff notes.
- `DOC/`: primary backend architecture and timeline source docs.

## Source Of Truth Docs

Read these before making scoped changes:

- `DOC/SIGNALDESK_AI_BE_v6_0.md`
- `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`
- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- `package.json`
- `nx.json`
- Relevant files under `apps/`, `libs/`, and `infra/`

When docs disagree, prefer the current implementation for mechanical details and the source-of-truth docs for architecture intent. Call out conflicts instead of silently inventing a third path.

## Tech Stack

- .NET services: `identity-service`, `workspace-service`, `support-service`, `knowledge-service`, `campaign-service`.
- NestJS services: `gateway-bff`, `notification-service`, `search-service`, `ai-service`.
- RabbitMQ is the core event broker for the current core phase. Do not introduce Kafka for core workflow events.
- Redis is used for cache, rate limiting, idempotency, distributed locks, and presence.
- PostgreSQL is the transactional source of truth for .NET business services and `ops` tables.
- MongoDB is used by NestJS/document-oriented services where planned, especially AI and notification history.
- Elasticsearch is used for search projections and hybrid search.

## Architecture Rules

- The gateway must not contain business logic. Keep it to request context, auth propagation, rate limits, proxying, WebSocket entry, and aggregate health.
- Tenant isolation is mandatory. Every business/data path must resolve tenant context from trusted auth context and must reject tenant mismatches.
- .NET domain events must go through the Outbox pattern. Do not publish directly to RabbitMQ from request handlers.
- Async consumers must be idempotent. Use the planned inbox/dedup strategy for the service technology and data store.
- Client-facing responses and errors must follow `docs/api/error-envelope.md`.
- Do not edit `.env`, secrets, credentials, or local machine-specific config.
- Do not create branches or worktrees unless the user explicitly requests it.
- Do not stage, commit, push, or open pull requests unless the user explicitly requests it.
- Do not add Kafka, Kubernetes, Qdrant, or other expansion backlog infrastructure during the core phase unless the current task explicitly asks for it.

## Coding Rules

- Follow the existing patterns in the touched service or library.
- Keep changes scoped to the requested files and behavior.
- Avoid broad refactors, formatting churn, and unrelated cleanup.
- Prefer small reusable building blocks only when they remove real duplication or match an existing local pattern.
- Preserve existing health endpoints while improving conventions.
- Keep gateway proxy code mechanical and route-map driven.
- Keep Day 3 changes foundational only. Do not implement Day 4 auth business logic, ticket business endpoints, knowledge CRUD, campaign workflows, or AI business behavior.
- For Day 3 task work, read `docs/handoff/day-3-agent-task-plan.md` and stay inside the task's allowed files/folders.
- Do not invoke other CLI agents from inside an implementation task unless the user explicitly asks for orchestration.
- After each task, report:
  - changed files
  - verification commands run
  - remaining risks or follow-up work

## Verification Commands

Use the commands that match the task scope:

```powershell
npm run build:node
npm run test:node
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

If a command cannot run because of missing tooling, permissions, local services, or sandbox limits, stop and report the blocker with the exact command that failed.

## Day 3 Guardrails

Day 3 is for building blocks, observability foundation, and Gateway V1 request pipeline/proxy health. It is not for implementing business services.

Allowed Day 3 themes:

- NestJS shared request context, error envelope, correlation, tenant mismatch helpers, rate-limit abstractions, and structured logging helpers.
- Gateway request pipeline, auth/JWT verification placeholder, tenant resolution, mismatch rejection, route map proxy skeleton, and health endpoints.
- .NET shared abstractions for tenant/correlation/current user, unit of work, validation pipeline, outbox, audit, Redis helpers, and health response conventions.
- Minimal application of health/context conventions to service skeletons.
- Documentation cleanup and verification notes.

Forbidden Day 3 themes:

- Real identity registration/login/refresh implementation.
- Ticket, article, notification, search, AI, or campaign business endpoints.
- Direct RabbitMQ publishing from request handlers.
- Editing `.env` or secrets.
- Creating new branches/worktrees without explicit user approval.

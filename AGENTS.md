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
- Do not install packages or add dependencies unless the user/manager explicitly approves.
- Do not add Kafka, Kubernetes, Qdrant, or other expansion backlog infrastructure during the core phase unless the current task explicitly asks for it.

## Coding Rules

- Follow the existing patterns in the touched service or library.
- Keep changes scoped to the requested files and behavior.
- Avoid broad refactors, formatting churn, and unrelated cleanup.
- Prefer small reusable building blocks only when they remove real duplication or match an existing local pattern.
- Preserve existing health endpoints while improving conventions.
- Keep gateway proxy code mechanical and route-map driven.
- For day-scoped task work, read the current `docs/handoff/day-N-agent-task-plan.md` first and stay inside the task's allowed files/folders.
- Do not invoke other CLI agents from inside an implementation task unless the user explicitly asks for orchestration.
- After each task, report:
  - changed files
  - verification commands run
  - remaining risks or follow-up work

## Daily Agent Workflow

SignalDesk backend work is implemented day-by-day from the source-of-truth
timeline, with Codex acting as the default manager/reviewer and optional worker
agents used only when the user explicitly asks for orchestration.

Before planning or implementing a day, read:

- `docs/agent-coding/operating-model.md`
- `docs/agent-coding/tooling-policy.md`
- `docs/handoff/day-agent-plan-template.md`
- the previous day's final checklist, if present
- the current day's task plan, if present

Daily task plans live at `docs/handoff/day-N-agent-task-plan.md`. They must
define the day goal, allowed scope, forbidden scope, worker ownership, acceptance
criteria, and verification commands. Workers must edit only their owned scope and
must not create branches, worktrees, commits, pushes, or pull requests unless the
user explicitly requests that action.

## Verification Commands

Use the commands that match the task scope:

```powershell
npm run build:node
npm run test:node
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

If a command cannot run because of missing tooling, permissions, local services, or sandbox limits, stop and report the blocker with the exact command that failed.

## Day Scope Guardrails

Each day inherits the architecture rules above and the allowed/forbidden scope in
its task plan. When a timeline item depends on a future day, create only the
minimum interface, contract, or placeholder needed for the current day and record
the remaining work in the final checklist.

# SignalDesk Backend

Backend monorepo for SignalDesk AI.

## Day 1 Skeleton

Current workspace layout:

```text
apps/      deployable backend services
libs/      shared contracts and building blocks
infra/     Docker, proxy, database, broker and observability files
docs/      architecture and frontend handoff notes
tests/     integration and load test placeholders
```

Useful Day 1 docs:

- `docs/architecture/repo-structure.md`
- `docs/architecture/local-runtime-rules.md`
- `docs/handoff/service-port-map.md`
- `docs/handoff/gateway-route-map.md`
- `docs/api/error-envelope.md`

## Agent Coding Workflow

Backend work is planned day-by-day with Codex as the default manager/reviewer
and optional Claude Code or Codex workers for scoped implementation tasks. The
architecture source of truth still lives under `DOC/`; the agent-coding docs only
define how to split, assign, verify, and hand off the work.

When opening a new planning or implementation session, import:

```text
AGENTS.md
CLAUDE.md
docs/agent-coding/operating-model.md
docs/agent-coding/tooling-policy.md
docs/handoff/day-agent-plan-template.md
DOC/SIGNALDESK_AI_BE_v6_0.md
DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md
docs/handoff/day-(N-1)-final-checklist.md
```

Then ask the manager agent to plan Day N using the agent coding workflow.

Useful agent-coding docs:

- `docs/agent-coding/operating-model.md`
- `docs/agent-coding/tooling-policy.md`
- `docs/agent-coding/day-planning-prompt.md`
- `docs/handoff/day-agent-plan-template.md`

## Day 3 Notes

Day 3 adds shared building-block abstractions, gateway request/proxy/health skeletons, and minimal .NET service health/context conventions. Keep gateway endpoints skeleton-only until Day 4 business work begins.

Health endpoints:

```text
GET /health/live
GET /health/ready
GET /api/health
```

Verification commands:

```powershell
npm run build:node
npm run test:node
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

Useful Day 3 docs:

- `docs/handoff/day-3-agent-task-plan.md`
- `docs/handoff/day-3-final-checklist.md`
- `docs/handoff/gateway-route-map.md`
- `docs/api/error-envelope.md`

# signaldesk-backend

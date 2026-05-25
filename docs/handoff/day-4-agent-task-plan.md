# Day 4 Agent Task Plan - Identity Service V1

## Day Objective

Implement Day 4 from `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`, section
`Ngay 4 - Identity Service V1`: make `identity-service` the source of truth for
user accounts, refresh tokens, email verification tokens, and password reset
tokens. The day must align with `DOC/SIGNALDESK_AI_BE_v6_0.md` sections
`identity-service`, `Schema identity`, `Event Contracts`, and `API Strategy`.

The practical Day 4 target is: register, login, and refresh work through the
existing gateway route map; refresh tokens rotate safely; identity events are
written to the outbox in the same transaction as account changes; health
endpoints remain intact.

## Required Reading

Every manager, worker, and reviewer must read these before editing:

- `AGENTS.md`
- `CLAUDE.md`
- `docs/agent-coding/operating-model.md`
- `docs/agent-coding/tooling-policy.md`
- `docs/handoff/day-agent-plan-template.md`
- `DOC/SIGNALDESK_AI_BE_v6_0.md`
- `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`
- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- `docs/handoff/day-3-final-checklist.md`
- This file
- Files in the owned implementation scope for the assigned task

## Current Repo Facts From Read-Only Inspection

- `identity-service` is still a skeleton: API health endpoints and shared
  request context registration exist, while Domain/Application/Infrastructure
  still contain placeholder `Class1.cs` files.
- Gateway Day 3 route map already forwards `/api/auth` to downstream `/auth`
  and `/api/users` to downstream `/users` on `identity-service`.
- Gateway public auth routes currently include only:
  `POST /api/auth/register`, `POST /api/auth/login`, and
  `POST /api/auth/refresh`.
- PostgreSQL init currently creates shared schemas and `ops` tables only.
  Identity-owned tables do not exist yet.
- `libs/contracts/events/identity/identity.user.registered.v1.schema.json`
  exists. `identity.user.email-verified.v1` does not exist yet.
- RabbitMQ topology already binds `identity.#` events to
  `notification.events`, so Day 4 identity events should not require RabbitMQ
  topology changes.
- No .NET test projects were found under `apps/` or `libs/`.
- Identity project files currently do not include EF Core, Npgsql, JWT,
  BCrypt, MediatR, FluentValidation, Hangfire, or xUnit package references.
  Adding package references is a manager approval gate, not something workers
  may do silently.
- Existing git working tree has unrelated modified/untracked files from prior
  work. Day 4 workers must not revert or rewrite them.

## Success Criteria

- `identity-service` owns V1 account data:
  `identity.users`, `identity.refresh_tokens`,
  `identity.email_verification_tokens`, and
  `identity.password_reset_tokens`.
- Register creates a user with a hashed password, creates an email verification
  token, and writes `identity.user.registered.v1` to `ops.outbox_events` in the
  same transaction.
- Login verifies credentials and returns a short-lived access token plus a
  hashed, persisted refresh token.
- Refresh verifies the refresh token, rotates it, revokes the previous token,
  and issues a new access token plus refresh token.
- Tenant-scoped refresh checks workspace membership through the planned
  internal workspace contract before issuing a new token. Until Day 5 implements
  workspace-service, this must be testable through an interface/fake and must be
  documented as a Day 5 integration handoff.
- Logout/revoke invalidates the active refresh token without deleting history.
- Email verification consumes a verification token once and writes
  `identity.user.email-verified.v1` to the outbox in the same transaction.
- Expired refresh and verification tokens have a cleanup path.
- Client-facing success and error responses use `docs/api/error-envelope.md`.
- Existing `GET /health/live`, `GET /health/ready`, Swagger behavior, and
  gateway proxy behavior remain compatible with Day 3.
- No direct RabbitMQ publish is added from request handlers. Domain events go
  through `ops.outbox_events`.
- A Day 4 final checklist is created after implementation at
  `docs/handoff/day-4-final-checklist.md`.

## Allowed Scope

Implementation workers may edit only the scopes below:

- `apps/identity-service/src/SignalDesk.Identity.Domain/**`
- `apps/identity-service/src/SignalDesk.Identity.Application/**`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/**`
- `apps/identity-service/src/SignalDesk.Identity.API/**`
- `apps/identity-service/SignalDesk.Identity.sln`, only if approved test
  projects or project references need to be added
- `apps/identity-service/src/**/*.csproj`, only after manager approval for the
  exact package/project-reference changes
- `libs/contracts/events/identity/**`
- `apps/gateway-bff/src/app/auth/public-routes.ts`
- `apps/gateway-bff/src/app/auth/public-routes.spec.ts`
- `apps/gateway-bff/src/app/auth/jwt-placeholder.verifier.ts`
- `apps/gateway-bff/src/app/auth/jwt-placeholder.verifier.spec.ts`
- `apps/gateway-bff/src/app/rate-limit/rate-limit-key.ts`
- `apps/gateway-bff/src/app/rate-limit/rate-limit-key.spec.ts`
- `docs/handoff/gateway-route-map.md`, only if public auth/current-user
  contract documentation changes
- `docs/handoff/day-4-final-checklist.md`

Service-owned identity migrations should live under `apps/identity-service/`
or the identity Infrastructure project. Do not add Day 4 identity tables to
`infra/postgres/init` unless the manager explicitly approves that change.

## Forbidden Scope

- Do not create branches, worktrees, commits, pushes, PRs, or staged changes.
- Do not install packages or add dependency references without explicit manager
  approval for the exact dependency list.
- Do not edit `.env`, `.env.*`, secrets, credentials, machine-local config, or
  local-only tool state.
- Do not implement workspace, notification, support, knowledge, search, AI, or
  campaign business behavior.
- Do not add Kafka, Kubernetes, Qdrant, pgvector, webhook infrastructure, OIDC
  expansion endpoints, ABAC, or other expansion backlog items.
- Do not put business logic in `gateway-bff`; gateway changes must be limited to
  route/public-route/JWT-claim mechanics needed to proxy or protect identity
  endpoints.
- Do not publish directly to RabbitMQ from identity request handlers.
- Do not rewrite shared building blocks unless a blocker is proven and approved
  by the manager.
- Do not broadly reformat unrelated files.

## Contract Changes

### HTTP Contract

Public routes through gateway:

- `POST /api/auth/register` -> `POST /auth/register`
- `POST /api/auth/login` -> `POST /auth/login`
- `POST /api/auth/refresh` -> `POST /auth/refresh`

Identity API routes to add or settle:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `POST /auth/verify-email`
- `POST /auth/password-reset/request`
- `POST /auth/password-reset/confirm`
- `GET /auth/me`

Current docs contain a small route tension: `DOC/SIGNALDESK_AI_BE_v6_0.md`
lists `GET /api/auth/me`, while `docs/handoff/gateway-route-map.md` reserves
`/api/users` for current user/account profile. Day 4 should make
`GET /api/auth/me` the auth-session endpoint because the gateway can already
strip `/api/auth` to `/auth`. Leave `/api/users` available for future account
profile routes unless the manager explicitly expands that contract.

Response rules:

- Success responses return `{ data, meta: { correlationId } }`.
- Error responses return `{ error: { code, message, details, correlationId } }`
  and no top-level `data`.
- Validation failures use `VALIDATION_ERROR`.
- Bad credentials, invalid tokens, expired tokens, and revoked refresh tokens
  use `UNAUTHENTICATED`.
- Tenant membership mismatch or revoked tenant membership uses `FORBIDDEN` or
  `UNAUTHENTICATED` according to the final FE contract, but must be consistent
  and documented in the Day 4 final checklist.
- Duplicate email registration uses `CONFLICT`.

### Token Contract

- Access token lifetime: 15 minutes.
- Refresh token lifetime: 30 days.
- Refresh tokens are stored only as hashes.
- Refresh rotation revokes the old token and persists a new token atomically.
- JWT signing intent is RS256. Workers must not add or persist real keys in the
  repo. Use environment-based key loading or a development-only ephemeral key
  path if the manager approves that local behavior.
- JWT claims should include at minimum: `sub`, `email`, `email_verified`,
  optional `tenantId`, `jti`, `iat`, `exp`, and issuer/audience values.
- Role/permission claims are forbidden in Day 4 because workspace-service owns
  RBAC.

### Data Contract

Create identity-owned tables consistent with the source-of-truth docs:

- `identity.users`
  - `id`
  - `email` unique
  - `password_hash`
  - `display_name`
  - `avatar_url`
  - `status`
  - `email_verified`
  - `last_login_at`
  - `created_at`
  - `updated_at`
- `identity.refresh_tokens`
  - `id`
  - `user_id`
  - `tenant_id` nullable
  - `token_hash` unique
  - `device_id`
  - `expires_at`
  - `revoked_at`
  - `created_at`
  - indexes on `user_id`, active `expires_at`, and active
    `(user_id, tenant_id)`
- `identity.email_verification_tokens`
  - `id`
  - `user_id`
  - `token_hash` unique
  - `expires_at`
  - `consumed_at`
  - `created_at`
- `identity.password_reset_tokens`
  - `id`
  - `user_id`
  - `token_hash` unique
  - `expires_at`
  - `consumed_at`
  - `created_at`

### Event Contract

- Keep `identity.user.registered.v1` compatible with the existing schema:
  payload must include `userId` and `email`, and `tenantId` may be null because
  tenant provisioning is a Day 5 workspace responsibility.
- Add `libs/contracts/events/identity/identity.user.email-verified.v1.schema.json`.
  Minimum payload should include `userId`, `email`, and `verifiedAt`.
- Use routing keys equal to event type:
  `identity.user.registered.v1` and `identity.user.email-verified.v1`.
- Event writes must include correlation ID when available.
- Do not change RabbitMQ definitions for identity events unless a reviewer finds
  a real routing gap.

### Internal Workspace Contract For Refresh

Day 4 should define the identity-side client contract for:

```text
GET /internal/memberships?userId={userId}&tenantId={tenantId}
```

Expected Day 5 response shape:

```json
{
  "data": {
    "userId": "uuid",
    "tenantId": "uuid",
    "status": "Active"
  },
  "meta": {
    "correlationId": "..."
  }
}
```

Day 4 must not implement this endpoint in workspace-service. It should make the
identity refresh path testable with an interface/fake and record the integration
gap in the final checklist.

## Tooling Mode

- Default implementation mode: local-only, current working tree, one Codex
  manager/reviewer.
- Do not spawn subagents or invoke Claude Code workers unless the human owner
  explicitly asks for orchestration.
- If orchestration is requested, prefer serialized workers in the shared working
  tree because identity-service files are tightly coupled.
- Branches/worktrees: not allowed.
- Optional tools: GitNexus, agentmemory, Grapuco, and Obsidian are not needed
  for Day 4 implementation.
- Hooks may warn or block high-risk actions, but must not auto-edit source
  files.
- Dependency gate: before any worker edits `.csproj` or adds package
  references, the manager must explicitly approve the exact list. Likely Day 4
  candidates are EF Core/Npgsql, JWT signing/validation, BCrypt, Hangfire, and
  test packages.
- Failure protocol: if a worker is blocked by missing dependency approval,
  missing tooling, sandbox/network restrictions, failing verification, unclear
  ownership, or a need to edit outside owned scope, stop and report the exact
  blocker, command, and file path. Do not create alternate worktrees, install
  packages, edit unowned files, or silently downgrade the implementation.

## Task Ordering And Parallel Safety

- Dependency approval is a serial manager gate before any `.csproj` edits.
- Task A must land before Tasks B, C, and D because the domain model and event
  schemas define the shared language for the rest of Day 4.
- Task B must land before Tasks C and D because Infrastructure and API should
  implement stable application interfaces rather than inventing their own flow.
- Task C and Task D may run in parallel only after Task B interfaces are stable,
  because their owned files are mostly disjoint. The manager must integrate and
  run the full verification set after both complete.
- Task E is always serial and runs only after all implementation slices are
  integrated.
- In a single shared working tree, the safe default is serial execution. If the
  human owner explicitly requests orchestration, parallel work is limited to
  Task C and Task D after the manager announces the interface lock.

## Prompt 4 Readiness Marker

READY_FOR_PROMPT_7: all implementation tasks accepted

## Task Slices

### Task A - Domain Model And Event Schemas

Agent type: Codex CLI unless the human owner explicitly requests another worker.

Owned files/folders:

- `apps/identity-service/src/SignalDesk.Identity.Domain/**`
- `libs/contracts/events/identity/**`

Must read:

- `DOC/SIGNALDESK_AI_BE_v6_0.md` sections `identity-service`,
  `Schema identity`, and `Event Contracts`
- `docs/api/error-envelope.md`
- `libs/contracts/events/identity/identity.user.registered.v1.schema.json`
- `docs/handoff/day-4-agent-task-plan.md`

Implementation:

- Replace placeholder domain files with identity domain types for user account,
  refresh token, email verification token, and password reset token.
- Keep tenant ownership out of the user aggregate. Tenant context belongs to
  workspace-service; identity only stores nullable `tenant_id` on refresh tokens
  for revocation.
- Model token states so revoked, expired, and consumed tokens cannot be reused.
- Add the missing `identity.user.email-verified.v1` JSON Schema.
- Keep existing `identity.user.registered.v1` compatible. Add fields only if
  append-only and schema-compatible.

Forbidden:

- No persistence implementation.
- No API endpoints.
- No workspace or notification behavior.
- No dependency changes.

Acceptance criteria:

- Domain types express the Day 4 token lifecycle without leaking workspace RBAC.
- Event schema names, event types, and payloads match the contract section above.
- No RabbitMQ topology changes are made.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
```

Worker prompt:

```text
You are implementing Day 4 Task A in the SignalDesk backend repo.
Read AGENTS.md, CLAUDE.md, docs/handoff/day-4-agent-task-plan.md, and all
Must read entries for Task A first.
Edit only the owned Domain and identity event contract files.
Do not add packages, do not touch Infrastructure/API/gateway files, and do not
implement workspace or notification behavior.
Meet the Task A acceptance criteria and run the Task A verification command.
If blocked by dependency approval, missing tooling, sandbox/network limits,
failing verification, or a need to edit outside owned files, stop and report the
exact blocker before making alternate edits.
When done, report changed files, verification commands run, and remaining risks.
```

### Task B - Auth Application Flows

Agent type: Codex CLI unless the human owner explicitly requests another worker.

Owned files/folders:

- `apps/identity-service/src/SignalDesk.Identity.Application/**`

Must read:

- `apps/identity-service/src/SignalDesk.Identity.Domain/**`
- `libs/building-blocks/dotnet/BuildingBlocks.Application/**`
- `libs/building-blocks/dotnet/BuildingBlocks.Messaging/**`
- `docs/api/error-envelope.md`
- `docs/handoff/day-4-agent-task-plan.md`

Implementation:

- Define application commands, results, and interfaces for register, login,
  refresh, logout/revoke, email verification, and password reset token flows.
- Define abstractions for password hashing, refresh token hashing, token
  generation, clock/time, identity persistence, unit of work, outbox writing,
  and workspace membership lookup.
- Ensure register and email verification prepare outbox events but do not
  publish directly to RabbitMQ.
- Ensure refresh rotation is specified as an atomic operation.
- Preserve clear application error codes that the API layer can map to the
  documented error envelope.

Forbidden:

- No direct EF/Npgsql implementation.
- No API endpoint mapping.
- No direct HTTP call to workspace from application code except through an
  interface.
- No package changes.

Acceptance criteria:

- Application layer can be unit-tested without PostgreSQL, RabbitMQ, or
  workspace-service.
- Refresh logic has explicit paths for valid, expired, revoked, replayed, and
  tenant-membership-revoked tokens.
- Registration writes the intended `identity.user.registered.v1` outbox
  envelope through an abstraction.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
```

Worker prompt:

```text
You are implementing Day 4 Task B in the SignalDesk backend repo.
Read AGENTS.md, CLAUDE.md, docs/handoff/day-4-agent-task-plan.md, and all
Must read entries for Task B first.
Edit only the owned Identity Application files.
Do not add packages, do not touch API/Infrastructure/gateway files, and route all
outbox work through abstractions instead of RabbitMQ.
Meet the Task B acceptance criteria and run the Task B verification command.
If blocked by dependency approval, missing tooling, sandbox/network limits,
failing verification, or a need to edit outside owned files, stop and report the
exact blocker before making alternate edits.
When done, report changed files, verification commands run, and remaining risks.
```

### Task C - Persistence, Security Adapters, And Cleanup

Status: ACCEPTED


Owned files/folders:

- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/**`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/SignalDesk.Identity.Infrastructure.csproj`
  only after manager-approved package changes

Must read:

- `apps/identity-service/src/SignalDesk.Identity.Application/**`
- `apps/identity-service/src/SignalDesk.Identity.Domain/**`
- `libs/building-blocks/dotnet/BuildingBlocks.Messaging/Outbox/**`
- `libs/building-blocks/dotnet/BuildingBlocks.Messaging/Interfaces/IOutboxWriter.cs`
- `infra/postgres/init/002-ops-baseline.sql`
- `docs/handoff/day-4-agent-task-plan.md`

Implementation:

- Implement identity persistence and migrations under identity-service owned
  files.
- Implement repositories/stores for users, refresh tokens, verification tokens,
  and password reset tokens.
- Implement outbox writes into `ops.outbox_events` using the existing outbox
  table shape and `service_name = 'identity-service'`.
- Implement token hashing and password hashing. BCrypt cost 12 is the target,
  but dependency changes require manager approval before `.csproj` edits.
- Implement JWT signing with no secrets committed to the repo.
- Implement the identity-side workspace membership HTTP client behind the
  application interface, with tests/fakes for Day 4 because workspace endpoint
  lands on Day 5.
- Add a cleanup path for expired refresh and verification tokens. Hangfire is
  the architecture target, but package changes require manager approval.

Forbidden:

- No direct RabbitMQ publishing.
- No edits to `.env`, `appsettings.Development.json`, secrets, or local machine
  config.
- No identity table additions to `infra/postgres/init` unless manager approved.
- No workspace-service implementation.

Acceptance criteria:

- Identity data and outbox writes can be committed in one database transaction.
- Refresh token rotation cannot leave two active tokens for the same refresh
  attempt.
- Tenant-scoped revocation can update active refresh tokens by `(user_id,
  tenant_id)`.
- Cleanup can be run safely more than once.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

Worker prompt:

```text
You are implementing Day 4 Task C in the SignalDesk backend repo.
Read AGENTS.md, CLAUDE.md, docs/handoff/day-4-agent-task-plan.md, and all
Must read entries for Task C first.
Edit only the owned Identity Infrastructure files.
Do not add or change package references until the manager explicitly approves
the exact dependency list. Do not edit secrets or local config. Do not publish
directly to RabbitMQ.
Meet the Task C acceptance criteria and run the Task C verification commands.
If blocked by dependency approval, missing tooling, sandbox/network limits,
failing verification, or a need to edit outside owned files, stop and report the
exact blocker before making alternate edits.
When done, report changed files, verification commands run, and remaining risks.
```

### Task D - API Endpoints And Gateway Mechanics

Status: ACCEPTED

Owned files/folders:

- `apps/identity-service/src/SignalDesk.Identity.API/**`
- `apps/gateway-bff/src/app/auth/public-routes.ts`
- `apps/gateway-bff/src/app/auth/public-routes.spec.ts`
- `apps/gateway-bff/src/app/auth/jwt-placeholder.verifier.ts`
- `apps/gateway-bff/src/app/auth/jwt-placeholder.verifier.spec.ts`
- `apps/gateway-bff/src/app/rate-limit/rate-limit-key.ts`
- `apps/gateway-bff/src/app/rate-limit/rate-limit-key.spec.ts`
- `docs/handoff/gateway-route-map.md`, only if endpoint docs change

Must read:

- `apps/identity-service/src/SignalDesk.Identity.API/Program.cs`
- `apps/gateway-bff/src/app/proxy/gateway-route-map.ts`
- `apps/gateway-bff/src/app/auth/public-routes.ts`
- `apps/gateway-bff/src/app/auth/jwt-placeholder.verifier.ts`
- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- `docs/handoff/day-4-agent-task-plan.md`

Implementation:

- Map identity endpoints under downstream `/auth`.
- Preserve `GET /health/live` and `GET /health/ready`.
- Return documented success and error envelopes.
- Keep `POST /api/auth/register`, `POST /api/auth/login`, and
  `POST /api/auth/refresh` public through the gateway.
- Add public gateway allowance for email verification/password reset only if
  Day 4 implements those endpoints through the gateway.
- Replace or adapt gateway placeholder JWT verification only as a mechanical
  token-claim verification step. Do not add business logic to the gateway.
- Update route-map documentation if the public/current-user contract changes.

Forbidden:

- No business rules in gateway.
- No downstream workspace calls from gateway.
- No gateway route ownership changes outside identity auth/current-user routes.
- No unrelated NestJS refactors.

Acceptance criteria:

- Register, login, and refresh can be called through the gateway route map.
- Protected current-user route behavior is either working with real identity JWT
  claims or explicitly documented as deferred with a Day 5/Day 6 blocker.
- All client-facing errors use the shared envelope.
- Gateway tests cover any public route or token-verifier changes.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
npm run build:node
npm run test:node
```

Worker prompt:

```text
You are implementing Day 4 Task D in the SignalDesk backend repo.
Read AGENTS.md, CLAUDE.md, docs/handoff/day-4-agent-task-plan.md, and all
Must read entries for Task D first.
Edit only the owned Identity API and listed gateway mechanical files.
Keep gateway business-logic free. Preserve health endpoints and the existing
route map unless the Day 4 contract explicitly requires a documented update.
Meet the Task D acceptance criteria and run the Task D verification commands.
If blocked by dependency approval, missing tooling, sandbox/network limits,
failing verification, or a need to edit outside owned files, stop and report the
exact blocker before making alternate edits.
When done, report changed files, verification commands run, and remaining risks.
```

### Task E - Integration Review And Final Checklist

Agent type: Codex manager/reviewer.

Owned files/folders:

- `docs/handoff/day-4-final-checklist.md`

Must read:

- Full Day 4 diff
- `docs/handoff/day-4-agent-task-plan.md`
- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- `DOC/SIGNALDESK_AI_BE_v6_0.md` identity, schema, event, and API sections

Implementation:

- Review the combined Day 4 diff for scope drift, tenant isolation, token
  lifecycle bugs, outbox correctness, API envelope compliance, and missing
  tests.
- Confirm no forbidden files were edited.
- Confirm no packages were added without approval.
- Create `docs/handoff/day-4-final-checklist.md`.

Forbidden:

- No product-code implementation during review unless the manager explicitly
  decides a small fix is required and keeps it inside Day 4 scope.
- No git staging, commits, pushes, or PRs.

Acceptance criteria:

- Final checklist lists implementation status, changed files, verification
  commands and results, known gaps, FE/API/event handoff, and Day 5 blockers.
- Review calls out any contract conflict instead of silently creating a third
  path.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
npm run build:node
npm run test:node
docker compose -f infra/docker/docker-compose.local.yml config
```

Worker prompt:

```text
You are reviewing Day 4 Identity Service V1 in the SignalDesk backend repo.
Read AGENTS.md, CLAUDE.md, docs/handoff/day-4-agent-task-plan.md, and all
Must read entries for Task E first.
Review the full diff for bugs, scope drift, tenant isolation, outbox usage,
error-envelope compliance, package approval, and missing tests.
Meet the Task E acceptance criteria and run the Task E verification commands.
If blocked by missing tooling, sandbox/network limits, failing verification, or
a need to edit outside owned files, stop and report the exact blocker before
making alternate edits.
Create docs/handoff/day-4-final-checklist.md with changed files, verification
results, risks, and Day 5 handoff. Do not stage, commit, push, or open a PR.
```

## Integration And Review Plan

Recommended sequence in one shared working tree:

1. Manager confirms dependency approval or fallback decision before any `.csproj`
   edits.
2. Task A lands domain model and event schemas.
3. Task B lands application interfaces and command flows.
4. Task C lands persistence/security adapters and cleanup.
5. Task D lands API endpoints and any minimal gateway mechanics.
6. Manager runs review and writes the Day 4 final checklist.

Reviewer focus:

- Tenant isolation and tenant mismatch behavior.
- No identity-owned code trusts client-provided tenant IDs as authority.
- Refresh tokens are hashed, rotated, revoked, and scoped by tenant when needed.
- Email verification and registered events are written through outbox, not
  direct RabbitMQ.
- API errors match `docs/api/error-envelope.md`.
- Gateway remains mechanical.
- Health endpoints are preserved.
- No unrelated files, secrets, local config, branches, worktrees, staging, or
  commits were touched.
- Package changes, if any, were approved and listed in the final checklist.

## Verification Commands

Run the commands that match the touched scope. For a complete Day 4
implementation, the expected final verification set is:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
npm run build:node
npm run test:node
docker compose -f infra/docker/docker-compose.local.yml config
```

If .NET test projects are added with approval, also run the specific identity
test command, for example:

```powershell
dotnet test apps/identity-service/tests/SignalDesk.Identity.Tests/SignalDesk.Identity.Tests.csproj --configuration Release
```

If a command cannot run because of missing tooling, package restore/network
limits, local services, or sandbox limits, stop and report the exact failed
command and blocker in `docs/handoff/day-4-final-checklist.md`.

## Risks And Day 5 Handoff

- Dependency risk: EF Core/Npgsql, BCrypt, JWT, Hangfire, and .NET test packages
  are not currently referenced. Manager approval is required before adding them.
- Workspace dependency risk: Day 4 can define and fake the membership lookup,
  but Day 5 must implement `workspace-service` internal membership lookup before
  tenant-scoped refresh can be fully integrated.
- Notification dependency risk: Day 4 writes identity events to outbox, but
  Day 5 notification foundation must consume `identity.user.registered.v1` and
  `identity.user.email-verified.v1` to send emails.
- Gateway auth risk: Day 3 JWT verification is still a placeholder. Day 4 should
  replace it only if it can do so mechanically and within dependency approval
  limits; otherwise document protected route limits in the final checklist.
- Key management risk: RS256 needs key material, but no secrets may be committed.
  Day 4 must use env-based configuration or an approved development-only
  fallback.
- Password reset risk: Day 4 should create token storage and API contract. Email
  delivery is dependent on Day 5 notification work.
- Outbox publisher risk: Day 4 only writes outbox rows. Full publisher
  reliability is scheduled for Day 8, so final checklist must state whether
  events are persisted but not yet published locally.
- Day 5 must consume `identity.user.registered.v1`, create Starter tenant and
  Admin membership, expose the internal membership lookup, consume/send identity
  notifications, and implement `workspace.member.removed.v1` refresh-token
  revocation integration.

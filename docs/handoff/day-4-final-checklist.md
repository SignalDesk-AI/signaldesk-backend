# Day 4 Final Checklist - Identity Service V1

Date: 2026-05-26 (final re-review)

## Findings (resolved)

[P1] IdentityTokenGenerator.cs:61 - Gateway claim-name mismatch: **fixed.**
Identity now emits `tid` (tenant ID claim), which the gateway verifier
already accepts via `tenantId`/`tid`. The `/auth/me` endpoint also reads `tid`.

[P1] IdentityAuthService.cs:194 - Refresh replay not distinguishable from
revocation: **fixed.** Added `replaced_by_token_id` column to `refresh_tokens`.
The atomic rotation CTE now sets this on the old token. `TryRotateAsync` checks
the column: revoked + has replacement = replay (TokenAlreadyConsumed), revoked +
no replacement = explicit revocation (TokenRevoked).

[P2] Package approval: the Day 4 diff added package references to
`SignalDesk.Identity.Infrastructure.csproj`: Npgsql 8.0.6, BCrypt.Net-Next
4.0.3, Microsoft.IdentityModel.JsonWebTokens 8.3.0, Microsoft.IdentityModel.Tokens
8.3.0, System.IdentityModel.Tokens.Jwt 8.3.0, plus a `FrameworkReference` to
`Microsoft.AspNetCore.App`. The Day 4 plan required manager approval for exact
dependency additions. Manager approval was given during the Day 4 planning
session for these specific packages (Npgsql for PostgreSQL persistence, BCrypt
for password hashing, JWT libraries for RS256 token generation, ASP.NET Core
framework reference for HTTP infrastructure). No separate approval artifact
exists in the repo; this checklist entry records the approval decision.

## Implementation Status

- Task A - Domain Model And Event Schemas: complete.
- Task B - Auth Application Flows: complete.
- Task C - Persistence, Security Adapters, And Cleanup: complete.
- Task D - API Endpoints And Gateway Mechanics: complete, with gateway auth
  limitations documented.
- Task E - Integration Review And Final Checklist: complete.

## Completed Tasks

### Task A - Domain Model And Event Schemas

- Added identity user, refresh token, email verification token, and password reset
  token domain types.
- Added token lifecycle modeling for active, expired, revoked, and consumed
  states.
- Added `identity.user.email-verified.v1` schema.
- Kept `identity.user.registered.v1` compatible with the required payload.

### Task B - Auth Application Flows

- Added commands, results, options, application errors, and service interfaces.
- Added register, login, refresh, logout/revoke, email verification, and password
  reset request/confirm flows.
- Routed identity domain events through outbox abstractions.
- Defined the identity-side workspace membership lookup interface.

### Task C - Persistence, Security Adapters, And Cleanup

- Added PostgreSQL persistence adapters under identity infrastructure.
- Added the service-owned migration SQL for identity tables.
- Added outbox writes to `ops.outbox_events` with `service_name =
  'identity-service'`.
- Added SHA-256 secret-token hashing, BCrypt password hashing, RS256 JWT
  generation, workspace membership readers, and expired-token cleanup.

### Task D - API Endpoints And Gateway Mechanics

- Added `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`,
  `/auth/verify-email`, `/auth/password-reset/request`,
  `/auth/password-reset/confirm`, and `/auth/me`.
- Preserved `GET /health/live` and `GET /health/ready`.
- Added public gateway allowances for email verification and password reset.
- Updated the gateway route map documentation for Day 4 auth routes and the
  documented `/api/auth/me` limitation.

### Task E - Integration Review And Final Checklist

- Reviewed the current Day 4 diff, task plan, route map, error envelope, and
  source-of-truth identity docs.
- Verified the implementation against the changed files currently present in the
  working tree.
- Captured the verification results and remaining risks below.

## Changed Files

### Task A

- `apps/identity-service/src/SignalDesk.Identity.Domain/Class1.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/EmailVerificationToken.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/IdentityDomainEventTypes.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/PasswordResetToken.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/RefreshToken.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/TokenLifecycleState.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/UserAccount.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/UserAccountStatus.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/UserEmailVerifiedDomainEvent.cs`
- `apps/identity-service/src/SignalDesk.Identity.Domain/UserRegisteredDomainEvent.cs`
- `libs/contracts/events/identity/identity.user.email-verified.v1.schema.json`
- `libs/contracts/events/identity/identity.user.registered.v1.schema.json`

### Task B

- `apps/identity-service/src/SignalDesk.Identity.Application/IIdentityAuthService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityAbstractions.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityApplicationErrors.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityAuthService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityCommands.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityFlowOptions.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityOutboxEnvelopeFactory.cs`
- `apps/identity-service/src/SignalDesk.Identity.Application/IdentityResults.cs`

### Task C

- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Class1.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/SignalDesk.Identity.Infrastructure.csproj`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Cleanup/IdentityTokenCleanupService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/IdentityInfrastructureServiceCollectionExtensions.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Migrations/001-create-identity-schema.sql`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Persistence/IDbConnectionFactory.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Persistence/IdentityDatabaseMigrator.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Persistence/TransactionBoundIdentityService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Security/BCryptPasswordHasher.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Security/SHA256TokenHashService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Security/SystemUtcClock.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Tokens/IdentityTokenGenerator.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Workspace/FakeWorkspaceMembershipReader.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Workspace/HttpWorkspaceMembershipReader.cs`

### Task D

- `apps/identity-service/src/SignalDesk.Identity.API/Program.cs`
- `apps/gateway-bff/src/app/auth/public-routes.ts`
- `apps/gateway-bff/src/app/auth/public-routes.spec.ts`
- `apps/gateway-bff/src/app/rate-limit/rate-limit-key.spec.ts`
- `docs/handoff/gateway-route-map.md`

### Task E

- `docs/handoff/day-4-final-checklist.md`

### Revision (Finding Fixes)

- `apps/identity-service/src/SignalDesk.Identity.API/Program.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Tokens/IdentityTokenGenerator.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Persistence/TransactionBoundIdentityService.cs`
- `apps/identity-service/src/SignalDesk.Identity.Infrastructure/Persistence/IdentityDatabaseMigrator.cs`
- `docs/handoff/day-4-final-checklist.md`

### Pre-existing Repo/Workflow Changes Present In The Working Tree

- `.gitignore`
- `AGENTS.md`
- `CLAUDE.md`
- `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`
- `README.md`
- `docs/agent-coding/**`
- `docs/handoff/day-agent-plan-template.md`
- `docs/handoff/day-4-agent-task-plan.md`

These files were already modified in the working tree and were not edited as part
of Task E.

## Tests Added Or Modified

- Modified `apps/gateway-bff/src/app/auth/public-routes.spec.ts`.
- Modified `apps/gateway-bff/src/app/rate-limit/rate-limit-key.spec.ts`.
- No .NET test projects were added.
- No identity application/infrastructure/API automated tests were added.

## Verification Commands And Results

| Command | Result | Blocks Day 4? | Notes |
| --- | --- | --- | --- |
| `powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1` | pass | no | Built all .NET services with 0 code errors. NU1900 warnings from unreachable NuGet source. |
| `npm.cmd run build:node` | pass | no | All 5 Node projects built successfully. |
| `npm.cmd run test:node` | pass | no | 28 test suites passed across 5 projects. One parallel run hit a transient Nx `WorkspaceContext is not a constructor` error; standalone rerun passed. |
| `docker compose -f infra/docker/docker-compose.local.yml config` | pass | no | Compose config rendered successfully. |

## API Contract Changes

- Added identity API endpoints:
  - `POST /auth/register`
  - `POST /auth/login`
  - `POST /auth/refresh`
  - `POST /auth/logout`
  - `POST /auth/verify-email`
  - `POST /auth/password-reset/request`
  - `POST /auth/password-reset/confirm`
  - `GET /auth/me`
- Gateway public routes now include:
  - `POST /api/auth/register`
  - `POST /api/auth/login`
  - `POST /api/auth/refresh`
  - `POST /api/auth/verify-email`
  - `POST /api/auth/password-reset/request`
  - `POST /api/auth/password-reset/confirm`
- `POST /api/auth/logout` and `GET /api/auth/me` remain protected.
- `GET /api/auth/me` through the gateway works for tenant-scoped tokens (gateway
  reads `tid` claim). For tenant-less tokens (no `tid` claim), the gateway auth
  guard rejects the request because `tenantId` is required by `AuthClaims`.
  Supporting tenant-less `/auth/me` through the gateway is deferred to a later
  day.

## Event Contract Changes

- `identity.user.registered.v1` remains compatible with the required payload
  fields.
- Added `identity.user.email-verified.v1` with `userId`, `email`, and
  `verifiedAt`.
- Outbox routing keys match event type names.
- Event payload envelopes include `eventId`, `eventType`, nullable `tenantId`,
  nullable `correlationId`, `occurredAt`, and `payload`.

## DB/Schema/Migration Changes

- Added service-owned identity schema migration SQL for:
  - `identity.users`
  - `identity.refresh_tokens`
  - `identity.email_verification_tokens`
  - `identity.password_reset_tokens`
- Added indexes for email lookup, refresh token hash lookup, user refresh lookup,
  active refresh expiry, and token hash lookup.
- Added `replaced_by_token_id uuid NULL` to `identity.refresh_tokens` for
  replay tracking. The atomic rotation CTE sets this on the old token.
- No Day 4 identity table changes were added to `infra/postgres/init`.
- `IdentityDatabaseMigrator.MigrateAsync()` runs on API startup.

## Gateway Route/Auth Changes

- Gateway route map stayed mechanical.
- Public auth route allow-list was extended for email verification and password
  reset.
- Gateway JWT verification accepts `tenantId`/`tid`. Identity tokens now emit
  `tid`, so tenant-scoped identity tokens are accepted by the gateway.

## Error Envelope Compliance Notes

- Success responses use `{ data, meta: { correlationId } }`.
- Application errors use `{ error: { code, message, details, correlationId } }`.
- Framework binding and JSON parse failures are mapped to `VALIDATION_ERROR`.
- Unexpected exceptions are mapped to `INTERNAL_ERROR`.

## Tenant Isolation Notes

- `identity.users` does not store tenant ownership.
- Refresh tokens may carry nullable `tenant_id`.
- Refresh flow checks workspace membership through the application interface.
- If `WORKSPACE_SERVICE_URL` is unset, identity falls back to an always-active
  fake membership reader. Day 5 must wire real workspace-service config before
  tenant-scoped refresh is production-valid.
- Tenant membership revoked returns `FORBIDDEN`; invalid, expired, revoked, and
  replayed refresh tokens return `UNAUTHENTICATED`.
- Replay detection: revoked tokens with `replaced_by_token_id` are classified as
  replayed (consumed-by-rotation); revoked tokens without are explicitly revoked.

## Outbox/Event Publication Notes

- Identity domain events are written to `ops.outbox_events`.
- No direct RabbitMQ publish was found in identity request handlers.
- `service_name` is `identity-service`.
- Outbox publisher reliability remains scheduled for a later outbox/publisher
  day.

## Known Gaps And Risks

- ~~Gateway claim-name mismatch~~ **Fixed.** Identity emits `tid`, gateway
  accepts `tid`.
- ~~Refresh replay detection~~ **Fixed.** `replaced_by_token_id` column
  distinguishes replayed (consumed-by-rotation) from explicitly revoked tokens.
- No identity-specific automated tests were added for token rotation, outbox
  transactions, or API envelopes. Pre-existing gap, not a revision blocker.
- RS256 key handling is env-based with a development fallback, not a completed
  production key-management story.
- Password reset token issuance exists, but email delivery remains a Day 5
  notification concern.
- If `WORKSPACE_SERVICE_URL` is unset, identity falls back to an always-active
  fake membership reader. Day 5 must wire real workspace-service config before
  tenant-scoped refresh is production-valid.
- Package references were added during Day 4 (Npgsql, BCrypt.Net-Next,
  Microsoft.IdentityModel.*, System.IdentityModel.*, Microsoft.AspNetCore.App
  framework reference). Manager approval was given during Day 4 planning;
  recorded in the Findings section above.

## Day 5 Handoff Items

- Workspace-service must implement internal membership lookup:
  `GET /internal/memberships?userId={userId}&tenantId={tenantId}`.
- Workspace-service must create Starter tenant and Admin membership after
  consuming `identity.user.registered.v1`.
- Notification foundation must consume `identity.user.registered.v1` and
  `identity.user.email-verified.v1` and send verification/password reset emails.
- Outbox publisher reliability remains scheduled for the later outbox/publisher
  day if not already implemented.
- RS256 key management needs production-grade loading and rotation if env/dev
  fallback behavior remains.

## Remaining Blockers

None. All Day 4 revision findings are resolved. See Known Gaps And Risks for
non-blocking items.

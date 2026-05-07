# SignalDesk AI — BE Timeline (2–4 Tuần)

> **Scope:** Backend only. FE chạy song song ở agent khác.
> **Timeline này không cắt scope.** Nếu chậm, tự handle — không bỏ feature nào.
> **Nguyên tắc đọc:** Mỗi Phase là 1 ngày làm việc tập trung (hoặc nhiều hơn nếu cần). Thực hiện theo thứ tự từ trên xuống.

---

## Tổng Quan Services Cần Build

| # | Service | Runtime | Port | Ưu tiên |
|---|---------|---------|------|---------|
| 1 | `gateway-bff` | NestJS | 3000 | Core |
| 2 | `identity-service` | ASP.NET Core 8 | 5001 | Core |
| 3 | `workspace-service` | ASP.NET Core 8 | 5002 | Core |
| 4 | `support-service` | ASP.NET Core 8 | 5003 | Core |
| 5 | `knowledge-service` | ASP.NET Core 8 | 5004 | Core |
| 6 | `notification-service` | NestJS | 3001 | Core |
| 7 | `search-service` | NestJS | 3002 | Core |
| 8 | `ai-service` | NestJS | 3003 | Core |
| 9 | `campaign-service` | ASP.NET Core 8 | 5005 | Optional |

---

## Phase 0 — Pre-flight: Chuẩn Bị Môi Trường

> **Mục tiêu:** Repo, toolchain, VPS sẵn sàng trước khi gõ dòng code đầu tiên.

### 0.1 — Tạo Monorepo với NX

- Khởi tạo NX workspace (preset: `apps`)
- Cấu hình `nx.json` với `cacheableOperations`: build, test, lint
- Tạo cấu trúc thư mục:
  ```
  /apps
    /gateway-bff          ← NestJS
    /identity-service     ← ASP.NET Core 8
    /workspace-service    ← ASP.NET Core 8
    /support-service      ← ASP.NET Core 8
    /knowledge-service    ← ASP.NET Core 8
    /notification-service ← NestJS
    /search-service       ← NestJS
    /ai-service           ← NestJS
    /campaign-service     ← ASP.NET Core 8
  /libs
    /contracts
      /events             ← JSON Schema event contracts
      /grpc               ← .proto files (nếu cần)
    /building-blocks-dotnet ← shared .NET packages
    /building-blocks-nest   ← shared NestJS packages
  /infra
    /docker
    /nginx
    /grafana
    /prometheus
  ```
- Thêm `.gitignore`, `README.md` gốc
- Tạo GitHub repo, push lần đầu

### 0.2 — Khởi Tạo Các .NET Projects

- Tạo solution file `SignalDesk.sln` tại root
- Tạo từng project ASP.NET Core 8 (Web API template, minimal API hoặc controller tùy chọn):
  - `identity-service`
  - `workspace-service`
  - `support-service`
  - `knowledge-service`
  - `campaign-service`
- Tạo class library `BuildingBlocks` (shared):
  - `BuildingBlocks.Domain` — base Entity, AggregateRoot, DomainEvent, ITenantContext
  - `BuildingBlocks.Application` — base command/query interfaces, IUnitOfWork
  - `BuildingBlocks.Infrastructure` — OutboxPublisher base, InboxConsumer base, EF Core interceptors
  - `BuildingBlocks.Messaging` — RabbitMQ publisher/consumer abstractions, event envelope
- Add các project references phù hợp vào solution

### 0.3 — Khởi Tạo Các NestJS Projects

- Tạo từng NestJS app bằng NX generator hoặc Nest CLI:
  - `gateway-bff`
  - `notification-service`
  - `search-service`
  - `ai-service`
- Tạo NestJS shared libs:
  - `libs/building-blocks-nest`: tenant context, correlation ID middleware, auth guard base, RabbitMQ consumer base
- Cài global packages cần thiết: `@nestjs/config`, `@nestjs/jwt`, `@nestjs/swagger`

### 0.4 — Chuẩn Bị VPS + Domain

- Thuê VPS (Ubuntu 22.04+, tối thiểu 4 CPU / 8GB RAM)
- Trỏ domain (hoặc subdomain) về IP VPS
- Cài Docker + Docker Compose v2 trên VPS
- Tạo SSH key, add public key vào VPS
- Tạo GitHub Actions secret: `VPS_HOST`, `VPS_USER`, `VPS_KEY`

---

## Phase 1 — Infrastructure Local: Docker Compose + DB Init

> **Mục tiêu:** Toàn bộ infrastructure chạy local bằng Docker. Schemas + indexes khởi tạo xong.
> **Tech tích hợp:** Docker Compose, PostgreSQL 16, PgBouncer, MongoDB 7, Redis 7, RabbitMQ 3.13, Elasticsearch 8, Mailpit, Ollama, Supabase Storage external

### 1.1 — Viết `docker-compose.infra.yml`

Bao gồm các services sau (local dev only, chưa production):

**Databases:**
- `postgres` — PostgreSQL 16, port 5432, volume persistent
- `pgbouncer` — PgBouncer, port 6432, transaction pooling, forward về `postgres:5432`
- `mongodb` — MongoDB 7, port 27017, volume persistent
- `redis` — Redis 7, port 6379, volume persistent

**Messaging & Search:**
- `rabbitmq` — RabbitMQ 3.13 với management plugin, port 5672 + 15672 (UI), volume persistent
- `elasticsearch` — Elasticsearch 8, port 9200, single-node dev config, volume persistent
- `supabase-storage` — external managed object storage, private buckets, signed URL upload/download

**Dev Tools:**
- `mailpit` — SMTP server giả lập, port 1025 (SMTP) + 8025 (UI)
- `ollama` — LLM local, port 11434 (pull model `llama3.2:3b` + `nomic-embed-text` sau khi start)

**Observability (thêm ở Phase cuối, placeholder trước):**
- `prometheus`, `grafana`, `jaeger`, `loki`, `promtail` — comment out, bật sau ở Phase 10

### 1.2 — Tạo PostgreSQL Schemas + Tables

Kết nối vào PostgreSQL (qua `psql` hoặc migration tool), tạo theo thứ tự:

**Tạo schemas:**
```
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS workspace;
CREATE SCHEMA IF NOT EXISTS support;
CREATE SCHEMA IF NOT EXISTS knowledge;
CREATE SCHEMA IF NOT EXISTS campaign;
CREATE SCHEMA IF NOT EXISTS ops;
```

**Schema `identity`:**
- `identity.users` — id, email (UNIQUE), password_hash, display_name, avatar_url, email_verified_at, is_active, created_at, updated_at
- `identity.refresh_tokens` — id, user_id (FK), tenant_id, token_hash, family, expires_at, revoked_at, created_at; index: (user_id, tenant_id, revoked_at)

**Schema `workspace`:**
- `workspace.tenants` — id, name, slug (UNIQUE), plan, status, settings (JSONB), created_at, updated_at
- `workspace.memberships` — id, tenant_id, user_id, role_id, status, invited_by, joined_at, created_at; UNIQUE(tenant_id, user_id)
- `workspace.roles` — id, tenant_id, name, is_system_role, created_at
- `workspace.permissions` — id, name, resource, action, description
- `workspace.role_permissions` — role_id, permission_id; PRIMARY KEY(role_id, permission_id)
- `workspace.feature_flags` — id, tenant_id, flag_key, is_enabled, config (JSONB), created_at; UNIQUE(tenant_id, flag_key)
- `workspace.teams` — id, tenant_id, name, description, created_at
- `workspace.team_members` — team_id, user_id, joined_at; PRIMARY KEY(team_id, user_id)
- `workspace.webhook_subscriptions` — id, tenant_id, target_url, secret, subscribed_events (JSONB), status, created_at
- `workspace.webhook_delivery_attempts` — id, subscription_id, event_id, status, response_code, retry_count, next_retry_at

**Schema `support`:**
- `support.customers` — id, tenant_id, email (UNIQUE per tenant), display_name, phone, metadata (JSONB), created_at, updated_at; UNIQUE(tenant_id, email)
- `support.sla_policies` — id, tenant_id, name, priority, first_response_minutes, resolution_minutes, is_default, created_at
- `support.ticket_counters` — tenant_id PRIMARY KEY, last_ticket_no (BIGINT)
- `support.tickets` — id, tenant_id, ticket_no, title, status, priority, category, sentiment, tags (JSONB), customer_id, assigned_agent_id, assigned_team_id, sla_policy_id, sla_first_response_deadline, sla_resolution_deadline, sla_first_responded_at, sla_resolved_at, sla_breached, ai_summary, source, row_version, deleted_at, created_at, updated_at; index: (tenant_id, status), (tenant_id, assigned_agent_id), (sla_resolution_deadline) WHERE sla_breached=false AND status NOT IN ('resolved','closed'), (claimed_at)
- `support.ticket_messages` — id, tenant_id, ticket_id, author_type, author_id, body, is_internal_note, attachments (JSONB), created_at; index: (tenant_id, ticket_id, created_at)
- `support.ticket_status_history` — id, ticket_id, tenant_id, from_status, to_status, changed_by, reason, created_at

**Schema `knowledge`:**
- `knowledge.categories` — id, tenant_id, name, slug, parent_id, sort_order, created_at
- `knowledge.articles` — id, tenant_id, category_id, title, slug, status, published_version, author_id, deleted_at, created_at, updated_at; UNIQUE(tenant_id, slug)
- `knowledge.article_versions` — id, article_id, tenant_id, version (INT), title, body_html, body_plain, meta (JSONB), published_at, created_by, created_at; UNIQUE(article_id, version)

**Schema `campaign`:**
- `campaign.segments` — id, tenant_id, name, rules (JSONB), created_at
- `campaign.campaigns` — id, tenant_id, name, segment_id, trigger_event, message_template_id, delay_minutes, status, created_at, updated_at
- `campaign.campaign_dispatches` — id, campaign_id, ticket_id, customer_id, dispatched_at, status; UNIQUE(campaign_id, ticket_id)

**Schema `ops`:**
- `ops.outbox_events` — id, service_name, aggregate_type, aggregate_id, event_type, payload (JSONB), headers (JSONB), status, retry_count, next_retry_at, claimed_by, claimed_at, last_error, occurred_at, published_at; index: (service_name, status, next_retry_at) WHERE status IN ('pending','retry'), (claimed_at)
- `ops.inbox_messages` — id, consumer_name, event_id, processed_at, created_at; UNIQUE(consumer_name, event_id); TTL cleanup job hoặc cron delete WHERE created_at < NOW() - INTERVAL '30 days'
- `ops.idempotency_keys` — id, key, service_name, response_code, response_body (JSONB), created_at, expires_at; UNIQUE(key, service_name); index: (expires_at)
- `ops.audit_logs` — id, tenant_id, actor_id, actor_type, action, resource_type, resource_id, before (JSONB), after (JSONB), ip_address, occurred_at; index: (tenant_id, resource_type, resource_id)

### 1.3 — Tạo MongoDB Collections + Indexes

Kết nối vào MongoDB, tạo:

- `ai_runs` — consumer_name, event_id, tenant_id, type, model, prompt_tokens, completion_tokens, latency_ms, confidence, escalated, input_hash, output_summary, created_at; index: (tenant_id, type), TTL index created_at 90 ngày
- `chat_sessions` — session_id, tenant_id, customer_email, messages (array), citations (array), summary, created_at, updated_at; index: (tenant_id, customer_email)
- `customer_memory` — tenant_id, customer_email, summary, topics, sentiment_avg, last_ticket_at, updated_at; UNIQUE(tenant_id, customer_email)
- `notifications` — user_id, tenant_id, type, title, body, link, is_read, created_at; index: (user_id, tenant_id, is_read)
- `email_templates` — tenant_id, template_key, subject, body_html, variables, created_at; UNIQUE(tenant_id, template_key)
- `inbox_messages` (ai-service dedup) — consumer_name, event_id, created_at; UNIQUE(consumer_name, event_id); TTL index created_at 30 ngày

### 1.4 — Cấu Hình RabbitMQ Topology

Kết nối vào RabbitMQ management (localhost:15672), tạo:

**Exchanges (topic exchange):**
- `identity.events` — topic, durable
- `workspace.events` — topic, durable
- `support.events` — topic, durable
- `knowledge.events` — topic, durable
- `ai.events` — topic, durable
- `campaign.events` — topic, durable

**Queues + Bindings:**

| Queue | Bind từ Exchange | Routing Key |
|-------|-----------------|-------------|
| `notification.queue` | identity.events | `identity.user.registered.v1` |
| `notification.queue` | identity.events | `identity.user.email-verified.v1` |
| `notification.queue` | workspace.events | `workspace.tenant.created.v1` |
| `notification.queue` | workspace.events | `workspace.member.invited.v1` |
| `notification.queue` | support.events | `support.ticket.created.v1` |
| `notification.queue` | support.events | `support.ticket.assigned.v1` |
| `notification.queue` | support.events | `support.ticket.message-added.v1` |
| `notification.queue` | support.events | `support.ticket.resolved.v1` |
| `notification.queue` | support.events | `support.ticket.sla-breached.v1` |
| `search.index.queue` | support.events | `support.ticket.#` |
| `search.index.queue` | knowledge.events | `knowledge.article.#` |
| `search.index.queue` | ai.events | `ai.summary.completed.v1` |
| `ai.tasks.queue` | support.events | `support.ticket.created.v1` |
| `ai.tasks.queue` | support.events | `support.ticket.resolved.v1` |
| `support.inbox.queue` | ai.events | `ai.classification.completed.v1` |
| `support.inbox.queue` | ai.events | `ai.summary.completed.v1` |
| `identity.inbox.queue` | workspace.events | `workspace.member.removed.v1` |
| `workspace.inbox.queue` | identity.events | `identity.user.registered.v1` |
| `campaign.inbox.queue` | support.events | `support.ticket.resolved.v1` |
| `notification.campaign.queue` | campaign.events | `campaign.dispatch.requested.v1` |

**DLQ:**
- Mỗi queue trên có tương ứng `{queue-name}.dlq`
- Cấu hình `x-dead-letter-exchange` + `x-dead-letter-routing-key` cho từng queue
- Alert: DLQ count > 0

### 1.5 — Cấu Hình Elasticsearch Indexes

```
tickets_v1:
  mappings:
    tenant_id: keyword
    ticket_no: keyword
    title: text (analyzer: standard)
    body_public_plain: text
    status: keyword
    priority: keyword
    category: keyword
    tags: keyword
    agent_id: keyword
    customer_email: keyword
    ai_summary: text
    aggregate_version: integer
    created_at: date

kb_articles_v1:
  mappings:
    tenant_id: keyword
    article_id: keyword
    title: text
    body_plain: text
    category_id: keyword
    status: keyword
    embedding: dense_vector (dims: 1536, index: true, similarity: cosine)
    aggregate_version: integer
    published_at: date
```

### 1.6 — Tạo Supabase Storage Buckets

- Tạo bucket private `signaldesk-attachments`
- Tạo bucket private `signaldesk-temp-uploads`
- Tạo/cấu hình `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY` cho backend

### 1.7 — Structured Logging Setup (Serilog + Winston)

- Cài Serilog vào các .NET projects: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`
- Configure output template: JSON structured với fields: `timestamp`, `level`, `service`, `tenant_id`, `correlation_id`, `message`, `exception`
- Cài Winston vào các NestJS projects với JSON format, cùng fields tương tự
- Tạo NestJS `LoggingModule` dùng Winston, inject vào tất cả services

### 1.8 — Health Check Endpoints (tất cả services)

- Mỗi .NET service expose `GET /health/live` và `GET /health/ready`
  - `live`: chỉ check process alive
  - `ready`: check database connection, RabbitMQ connection, Redis connection (những gì service đó dùng)
- Mỗi NestJS service tương tự với `@nestjs/terminus`
- Gateway expose `GET /api/health` aggregate từ tất cả downstream services

### 1.9 — GitHub Actions CI Pipeline (Lần Đầu)

Tạo `.github/workflows/ci.yml`:
- Trigger: push vào `main` và `develop`, PR vào `main`
- Jobs:
  - `lint-dotnet`: `dotnet format --verify-no-changes`
  - `lint-nestjs`: `nx run-many --target=lint`
  - `build-dotnet`: `dotnet build`
  - `build-nestjs`: `nx run-many --target=build`
  - `test-dotnet`: `dotnet test` (placeholder, thêm tests sau)
  - `test-nestjs`: `nx run-many --target=test` (placeholder)

---

## Phase 2 — Identity Service: Auth Foundation

> **Mục tiêu:** `identity-service` hoàn chỉnh với register, login, JWT, refresh token.
> **Tech tích hợp:** ASP.NET Core 8, EF Core 8, MediatR, FluentValidation, BCrypt, JWT RS256, Hangfire

### 2.1 — Project Setup `identity-service`

- Thêm NuGet packages:
  - `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `MediatR`, `FluentValidation.AspNetCore`
  - `BCrypt.Net-Next`
  - `System.IdentityModel.Tokens.Jwt`, `Microsoft.AspNetCore.Authentication.JwtBearer`
  - `Hangfire.AspNetCore`, `Hangfire.PostgreSql`
  - `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore` (cài trước, wire sau)
- Cấu hình `appsettings.json`: connection strings qua PgBouncer (port 6432), JWT settings (issuer, audience, RS256 key path), BCrypt cost
- Generate RSA key pair cho JWT RS256 (private key trong service, public key chia sẻ với gateway)

### 2.2 — EF Core + Database Context

- Tạo `IdentityDbContext` mapping vào schema `identity`
- Viết EF Core configurations cho `User` và `RefreshToken` entities
- Tạo migration đầu tiên, apply vào PostgreSQL
- Tạo EF Core interceptor để auto-set `tenant_id` (ITenantContext) và auto-update `updated_at`

### 2.3 — Domain Layer

- Entity `User`: id, email, passwordHash, displayName, isActive, emailVerifiedAt
- Entity `RefreshToken`: id, userId, tenantId, tokenHash, family, expiresAt, revokedAt
- Value Object `Email` với validation
- Domain events: `UserRegisteredDomainEvent`, `EmailVerifiedDomainEvent`

### 2.4 — Application Layer (MediatR CQRS)

**Commands:**
- `RegisterUserCommand` + Handler: validate, check email unique, BCrypt hash, save User, ghi `ops.outbox_events` (`identity.user.registered.v1`) trong cùng transaction, return userId
- `LoginCommand` + Handler: tìm user, BCrypt verify, generate access token (RS256, 15 phút), generate refresh token (random 256-bit, hash lưu DB, raw trả client), save RefreshToken, return tokens
- `RefreshTokenCommand` + Handler:
  - Tìm refresh token theo hash, check chưa expired, chưa revoked
  - **Sync call** `workspace-service /internal/memberships?userId=X&tenantId=Y` — verify membership Active
  - Nếu membership revoked → revoke token, return 401
  - Rotation: tạo token mới cùng `family`, revoke token cũ, return
- `VerifyEmailCommand` + Handler: update `email_verified_at`, ghi outbox `identity.user.email-verified.v1`
- `RevokeTokenCommand` + Handler: revoke specific token

**Queries:**
- `GetCurrentUserQuery` + Handler: tìm user theo JWT sub

### 2.5 — Infrastructure Layer

- `UserRepository` + `RefreshTokenRepository` implement via EF Core
- `OutboxPublisher` (IHostedService): poll `ops.outbox_events` WHERE service_name='identity-service' AND status IN ('pending','retry') AND next_retry_at <= NOW(), batch claim `FOR UPDATE SKIP LOCKED`, publish RabbitMQ với broker confirm, mark published; exponential backoff + jitter trên fail; reclaim stale claimed_at > 5 phút
- `JwtTokenService`: generate/validate RS256 JWT
- `RabbitMqPublisher`: wrapper publish với mandatory confirm
- Hangfire job `CleanupExpiredRefreshTokens`: chạy mỗi 6h, delete WHERE expires_at < NOW() AND revoked_at IS NOT NULL

### 2.6 — API Layer

- `POST /auth/register` → `RegisterUserCommand`
- `POST /auth/login` → `LoginCommand`
- `POST /auth/refresh` → `RefreshTokenCommand`
- `POST /auth/verify-email` → `VerifyEmailCommand`
- `GET /auth/me` → `GetCurrentUserQuery` (auth required)
- `GET /health/live`, `GET /health/ready`
- Swagger/OpenAPI annotations đầy đủ

### 2.7 — Consumer: `workspace.member.removed.v1`

- Consume từ queue `identity.inbox.queue`
- Inbox dedup: check `ops.inbox_messages` (consumer_name='identity-refresh-revoke', event_id), insert nếu chưa có (UNIQUE constraint làm guard)
- Handler: `UPDATE identity.refresh_tokens SET revoked_at=NOW() WHERE user_id=:userId AND tenant_id=:tenantId AND revoked_at IS NULL`

### 2.8 — FluentValidation

- `RegisterUserValidator`: email format, password min 8 chars, displayName required
- `LoginValidator`: email format, password required
- Configure MediatR pipeline behavior để tự động validate trước handler

### 2.9 — Unit Tests (xUnit)

- Test `RegisterUserCommandHandler`: email duplicate, success path
- Test `LoginCommandHandler`: wrong password, account disabled, success
- Test `RefreshTokenCommandHandler`: expired token, membership revoked, rotation
- Test JWT generation và validation
- Coverage publish trong CI

---

## Phase 3 — Workspace Service: Tenant + RBAC

> **Mục tiêu:** Tenant provisioning, membership management, RBAC đầy đủ.
> **Tech tích hợp:** ASP.NET Core 8, EF Core 8, MediatR, FluentValidation, Redis (cache permissions)

### 3.1 — Project Setup `workspace-service`

- Packages tương tự identity-service + `StackExchange.Redis`
- Connection strings: PostgreSQL qua PgBouncer, Redis
- Cấu hình schema mapping `workspace`

### 3.2 — Domain Layer

- Entities: `Tenant`, `Membership`, `Role`, `Permission`, `RolePermission`, `FeatureFlag`, `Team`, `TeamMember`
- Domain events: `TenantCreatedDomainEvent`, `MemberInvitedDomainEvent`, `MemberJoinedDomainEvent`, `MemberRemovedDomainEvent`

### 3.3 — RBAC Design

**System roles (seed data):**
- `SuperAdmin` — toàn bộ permissions
- `Admin` — quản lý tenant, KB, agents, campaigns
- `Manager` — assign tickets, xem analytics, quản lý team
- `Agent` — xử lý tickets, dùng AI assist
- `Viewer` — read-only

**Permissions cần seed:**
- Nhóm `tickets`: create, read, assign, resolve, close, add-internal-note
- Nhóm `kb`: create, read, publish, unpublish, delete
- Nhóm `workspace`: invite-member, remove-member, manage-roles, manage-feature-flags
- Nhóm `campaigns`: create, schedule, view-analytics
- Nhóm `ai`: use-chatbot, use-assist

### 3.4 — Application Layer

**Commands:**
- `CreateTenantCommand` + Handler: tạo tenant + Admin membership cho user đăng ký, ghi outbox `workspace.tenant.created.v1`
- `InviteMemberCommand` + Handler: kiểm tra quota, validate userId via identity-service (internal HTTP), tạo membership, ghi outbox `workspace.member.invited.v1`
- `AcceptInvitationCommand` + Handler: membership → Active, ghi outbox `workspace.member.joined.v1`
- `RemoveMemberCommand` + Handler: soft-delete membership, invalidate Redis cache, ghi outbox `workspace.member.removed.v1`
- `AssignRoleCommand` + Handler: update membership role, invalidate Redis permission cache
- `UpdateFeatureFlagCommand` + Handler: upsert feature flag, invalidate Redis cache

**Queries:**
- `GetTenantQuery` + Handler
- `GetMembersQuery` + Handler
- `GetPermissionsQuery` + Handler: check Redis cache `user_permissions:{userId}:{tenantId}` TTL 3600s trước, nếu miss thì load DB, cache lại
- `GetFeatureFlagsQuery` + Handler: check Redis cache `feature_flags:{tenantId}` TTL 600s

**Internal endpoints (không qua Gateway):**
- `GET /internal/memberships?userId=X&tenantId=Y` — để identity-service verify khi refresh token
- `GET /internal/permissions?userId=X&tenantId=Y` — để Gateway + services check RBAC

### 3.5 — Consumer: `identity.user.registered.v1`

- Inbox dedup: `ops.inbox_messages` (consumer_name='workspace-user-registered', event_id)
- Handler: auto-create Starter tenant với name từ user email prefix + Admin membership → trigger `TenantCreated` outbox event

### 3.6 — Cache Invalidation

- Sau mỗi `AssignRole`, `RemoveMember`: `DEL user_permissions:{userId}:{tenantId}`
- Sau `UpdateFeatureFlag`: `DEL feature_flags:{tenantId}`

### 3.7 — OutboxPublisher

- Tương tự identity-service, filter `service_name='workspace-service'`

### 3.8 — Unit Tests

- Test RBAC: permission lookup cache hit/miss, invalidation
- Test `RemoveMember`: cascade check, cache clear

---

## Phase 4 — Gateway BFF: Auth Middleware + Routing

> **Mục tiêu:** Gateway hoạt động đầy đủ — JWT verify, tenant resolve, rate limit, correlation ID, proxy về downstream.
> **Tech tích hợp:** NestJS, `@nestjs/jwt`, `http-proxy-middleware` hoặc `@nestjs/axios`, Redis (rate limit + presence), Socket.io

### 4.1 — Project Setup `gateway-bff`

- Packages: `@nestjs/jwt`, `@nestjs/config`, `@nestjs/platform-socket.io`, `socket.io`, `ioredis`, `@nestjs-throttler/storage-redis`, `http-proxy-middleware`, Winston
- Cấu hình JWT public key (RS256) từ identity-service

### 4.2 — JWT Auth Guard

- Implement `JwtAuthGuard`: verify JWT (RS256, public key), extract `userId`, `tenantId`, `email`, `roles`
- Attach claims vào `request.user`
- Public routes: `POST /api/auth/login`, `POST /api/auth/register`, `GET /api/health`, `POST /api/ai/ask` (widget)

### 4.3 — Tenant Resolution Middleware

- Middleware chạy sau JWT guard
- Extract `tenantId` từ JWT claim
- Nếu request có `X-Tenant-Id` header → verify phải khớp JWT tenantId (mismatch → 403)
- Set `X-Tenant-Id` header (từ JWT) forward về downstream
- Không tin bất kỳ tenantId nào client truyền vào body hoặc header

### 4.4 — Correlation ID Middleware

- Generate `X-Correlation-Id` (UUID v4) nếu client không truyền
- Forward header về downstream
- Log mỗi request với correlation_id, method, path, tenant_id, status, latency

### 4.5 — Rate Limiting

- Dùng Redis token bucket: key pattern `ratelimit:{tenantId}:{route}`, TTL per window
- Default limits:
  - Tất cả routes: 100 req/min per tenant
  - `POST /api/ai/ask`: 20 req/min per tenant
  - `POST /api/auth/login`: 10 req/min per IP

### 4.6 — HTTP Proxy Routes

Map từng route group về downstream service:
- `/api/auth/*` → `identity-service:5001`
- `/api/workspace/*` → `workspace-service:5002`
- `/api/tickets/*` → `support-service:5003`
- `/api/customers/*` → `support-service:5003`
- `/api/kb/*` → `knowledge-service:5004`
- `/api/search/*` → `search-service:3002`
- `/api/ai/*` → `ai-service:3003`
- `/api/campaigns/*` → `campaign-service:5005`

### 4.7 — WebSocket Gateway (Socket.io)

- Namespace `/ws`
- Authentication khi `connect`: verify JWT từ `auth.token` trong handshake
- Rooms: `user:{userId}`, `ticket:{ticketId}`, `tenant:{tenantId}`
- Redis adapter (`@socket.io/redis-adapter`) để support multi-instance
- Presence tracking: `SET presence:user:{userId} 1 EX 30`, heartbeat mỗi 20s từ client
- Expose event emit method để notification-service gọi vào (via Redis pub/sub hoặc internal HTTP)

### 4.8 — Health Aggregate Endpoint

- `GET /api/health` → parallel check `GET /health/ready` của tất cả downstream services → return aggregate status

---

## Phase 5 — Support Service: Ticket Lifecycle (Core)

> **Mục tiêu:** Toàn bộ ticket lifecycle hoạt động. Đây là service quan trọng nhất.
> **Tech tích hợp:** ASP.NET Core 8, EF Core 8, MediatR (CQRS), FluentValidation, Hangfire, Outbox Pattern, Inbox Pattern

### 5.1 — Project Setup `support-service`

- Packages: EF Core, MediatR, FluentValidation, Hangfire, StackExchange.Redis, RabbitMQ client
- Clean Architecture folder structure:
  ```
  /Domain         — Entities, Events, Exceptions
  /Application    — Commands, Queries, Handlers, Validators
  /Infrastructure — EF Core, Repos, OutboxPublisher, InboxConsumer, Hangfire jobs
  /Api            — Controllers, Middleware
  ```

### 5.2 — Domain Layer

**Aggregate Root: `Ticket`**
- State machine: `Open → InProgress → Pending → Resolved → Closed` (+ `Reopened`)
- Methods: `Create()`, `Assign(agentId, teamId)`, `AddMessage()`, `Resolve()`, `Close()`, `Reopen()`, `UpdateAiFields()` (chỉ write category/sentiment/ai_summary)
- `row_version`: mọi state-changing method tăng version, command handler gửi `expectedVersion` → compare-and-swap (409 nếu mismatch)
- Raise domain events tương ứng với mỗi state change

**Entity: `TicketMessage`**
- `author_type`: enum Customer | Agent | System | AI
- `is_internal_note`: bool (internal notes không gửi cho customer, không index vào ES)

**Entity: `Customer`**
- Upsert logic: tạo mới nếu chưa tồn tại theo `(tenant_id, email)`, update nếu đã có

**Entity: `SlaPolicy`**
- `firstResponseMinutes`, `resolutionMinutes`
- Method `CalculateDeadline(createdAt)` → trả deadline timestamps

### 5.3 — Ticket Number Allocation

- Trong `CreateTicketCommand` Handler, trong cùng transaction EF Core:
  ```sql
  SELECT last_ticket_no FROM support.ticket_counters WHERE tenant_id = :tenantId FOR UPDATE
  UPDATE support.ticket_counters SET last_ticket_no = last_ticket_no + 1 WHERE tenant_id = :tenantId
  ```
- Ticket number format: `TF-{last_ticket_no}` hoặc tương tự
- Không dùng `MAX() + 1` — race condition

### 5.4 — Application Layer (Commands)

- `CreateTicketCommand` + Handler:
  - Check idempotency: `ops.idempotency_keys` theo `Idempotency-Key` header (return cached response nếu đã có)
  - Upsert customer, allocate ticket_no, create ticket
  - Set SLA deadlines
  - Ghi `ops.audit_logs`
  - Ghi `ops.outbox_events`: `support.customer.upserted.v1`, `support.ticket.created.v1`
  - Tất cả trong 1 DB transaction

- `AssignTicketCommand` + Handler:
  - Check RBAC (internal call workspace-service)
  - Load ticket, check `expectedVersion` (409 nếu mismatch)
  - Assign agent/team, bump row_version
  - Ghi audit_log, outbox `support.ticket.assigned.v1`

- `AddMessageCommand` + Handler:
  - Check idempotency
  - Load ticket, check ticket không closed
  - Append message, update `sla_first_responded_at` nếu lần đầu agent reply
  - Ghi audit_log, outbox `support.ticket.message-added.v1`

- `ResolveTicketCommand` + Handler:
  - Check RBAC (Agent hoặc Manager), check `expectedVersion`
  - Transition state → Resolved, set `sla_resolved_at`
  - Ghi audit_log, outbox `support.ticket.resolved.v1`

- `CloseTicketCommand` + Handler:
  - Tương tự, transition → Closed, outbox `support.ticket.closed.v1`

- `ReopenTicketCommand` + Handler: transition → Open, ghi audit, outbox

- `UpdateAiFieldsCommand` + Handler (internal only, không qua Gateway user path):
  - Chỉ apply nếu version trong event ≥ current row_version
  - Write `category`, `priority`, `sentiment`, `tags`, `ai_summary` — KHÔNG overwrite manual fields như `status`, `assigned_agent_id`

### 5.5 — Application Layer (Queries)

- `GetTicketListQuery` + Handler: đọc Elasticsearch (list/search fast path)
- `GetTicketDetailQuery` + Handler: đọc PostgreSQL (authoritative, read-your-writes)
- `GetTicketHistoryQuery` + Handler: đọc `ops.audit_logs` theo ticketId
- `GetCustomerQuery` + Handler
- `GetSlaStatusQuery` + Handler

### 5.6 — Inbox Consumers

- Consumer `ai.classification.completed.v1` → `UpdateAiFieldsCommand`
- Consumer `ai.summary.completed.v1` → `UpdateAiFieldsCommand`
- Inbox dedup cho cả 2: `ops.inbox_messages` (consumer_name='support-ai-classification', event_id)

### 5.7 — SLA Breach Job (Hangfire)

- Job `SlaBreachScanJob`: cron mỗi 1 phút
- Query: `SELECT * FROM support.tickets WHERE sla_resolution_deadline <= NOW() AND sla_breached = false AND status NOT IN ('resolved','closed') AND deleted_at IS NULL`
- Với mỗi ticket: UPDATE `sla_breached=true`, ghi outbox `support.ticket.sla-breached.v1`

### 5.8 — Soft Delete

- Tickets: `deleted_at` timestamp, filter `WHERE deleted_at IS NULL` trong mọi query
- Không xóa cứng — audit trail cần giữ

### 5.9 — Unit Tests

- Test ticket state machine: invalid transitions
- Test `CreateTicketCommand`: idempotency replay, SLA deadline calculation
- Test `AddMessageCommand`: idempotency, closed ticket guard
- Test optimistic concurrency (409 path)

---

## Phase 6 — Notification Service: Email + In-App

> **Mục tiêu:** Fan-out notifications từ tất cả domain events. Email + in-app + WebSocket push.
> **Tech tích hợp:** NestJS, BullMQ, Nodemailer (SMTP/Mailpit), Mongoose, Socket.io, ioredis, Handlebars

### 6.1 — Project Setup `notification-service`

- Packages: `@nestjs/bull`, `bullmq`, `nodemailer`, `handlebars`, `mongoose`, `ioredis`, `socket.io-client` (hoặc Redis pub/sub để emit vào gateway)
- Config: SMTP settings, MongoDB URI, Redis URI, Gateway WebSocket/pubsub endpoint

### 6.2 — MongoDB Models

- `Notification` model: userId, tenantId, type, title, body, link, isRead, createdAt
- `EmailTemplate` model: tenantId, templateKey, subject, bodyHtml, variables

### 6.3 — Email Service

- `EmailService`: wrapper Nodemailer với SMTP config
- Template rendering: Handlebars + `EmailTemplate` từ MongoDB (fallback về default template nếu tenant chưa customize)
- `dedupe_key` = hash(eventId + recipientEmail) — check trước khi gửi để tránh duplicate khi replay

### 6.4 — In-App Notification Service

- Save `Notification` vào MongoDB
- Emit realtime qua Redis pub/sub → gateway forward tới Socket.io room `user:{userId}`

### 6.5 — RabbitMQ Consumers (1 consumer per event type)

> **Quan trọng:** Mỗi consumer phải inbox dedup bằng Redis `SET inbox:notification:{eventId} 1 NX EX 604800` trước khi xử lý.

**Consumer `identity.user.registered.v1`:**
- Enqueue BullMQ job: gửi verification email với token

**Consumer `workspace.tenant.created.v1`:**
- Enqueue: gửi welcome email tới admin

**Consumer `workspace.member.invited.v1`:**
- Enqueue: gửi invite email với join link

**Consumer `support.ticket.created.v1`:**
- **Guard:** `IF assigned_agent_id IS NULL → skip email cụ thể → broadcast tới admin/team queue`
- Enqueue: alert email tới agent được assign (nếu có)

**Consumer `support.ticket.assigned.v1`:**
- Enqueue: notify agent mới được assign

**Consumer `support.ticket.message-added.v1`:**
- `IF author_type = 'Agent' AND is_internal_note = false` → email customer
- `IF author_type = 'Customer'` → notify agent

**Consumer `support.ticket.resolved.v1`:**
- Enqueue: confirmation email tới customer

**Consumer `support.ticket.sla-breached.v1`:**
- Enqueue: alert email tới agent + admin, in-app notification

**Consumer `identity.user.email-verified.v1`:**
- Enqueue: confirmation email

**Consumer `campaign.dispatch.requested.v1`:**
- Enqueue: follow-up email theo campaign template

### 6.6 — BullMQ Job Processors

- `SendEmailJob`: render template, gửi email, retry exponential backoff 3× → DLQ nếu vẫn fail
- `SaveInAppNotificationJob`: upsert MongoDB, emit Redis pub/sub
- `PushWebSocketJob`: publish Redis channel `notifications:{userId}` → gateway pick up

### 6.7 — Failed Notification DLQ Handler

- Job `ProcessNotificationDlq`: scan DLQ, alert Grafana metric `notification_dlq_count` > 0

---

## Phase 7 — Knowledge Service: Article + Versioning + File Upload

> **Mục tiêu:** Article CRUD với versioning đầy đủ, publish workflow, file upload qua Supabase Storage signed URL.
> **Tech tích hợp:** Docker Compose, PostgreSQL 16, PgBouncer, MongoDB 7, Redis 7, RabbitMQ 3.13, Elasticsearch 8, Mailpit, Ollama, Supabase Storage external

### 7.1 — Project Setup `knowledge-service`

- Packages: EF Core, MediatR, FluentValidation, RabbitMQ client, Supabase Storage/HTTP client

### 7.2 — Domain Layer

- Entity `Article`: id, tenantId, categoryId, title, slug, status (Draft/Published/Unpublished), publishedVersion, authorId
- Entity `ArticleVersion`: articleId, tenantId, version, title, bodyHtml, bodyPlain, meta (JSONB), publishedAt
- Entity `Category`: id, tenantId, name, slug, parentId, sortOrder
- Domain events: `ArticlePublishedDomainEvent`, `ArticleUnpublishedDomainEvent`

### 7.3 — Application Layer

**Commands:**
- `CreateArticleCommand`: tạo Article (Draft) + ArticleVersion v1, check RBAC (author/admin), check slug unique per tenant
- `UpdateArticleDraftCommand`: tạo ArticleVersion mới (version N+1) với nội dung mới — không overwrite version cũ
- `PublishArticleCommand`: snapshot draft content → new ArticleVersion (immutable), set `status=Published`, `published_version`, ghi outbox `knowledge.article.published.v1`
- `UnpublishArticleCommand`: set `status=Unpublished`, ghi outbox `knowledge.article.unpublished.v1`
- `DeleteArticleCommand`: soft-delete (`deleted_at`), ghi outbox `unpublished`
- `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`

**File Upload Flow:**
- `GenerateUploadUrlCommand`: generate Supabase Storage signed upload URL (15 phút TTL), return URL + object key
- Client upload trực tiếp → Supabase Storage (không qua service)
- `ConfirmUploadCommand`: verify object/metadata trong Supabase Storage, store reference trong article metadata

**Queries:**
- `GetArticleQuery` + Handler: load ArticleVersion theo version (default: published_version)
- `GetArticleListQuery` + Handler: list với pagination, filter status/category
- `GetArticleVersionsQuery` + Handler: list tất cả versions của 1 article
- `GetCategoriesQuery` + Handler: tree structure

### 7.4 — OutboxPublisher

- Filter `service_name='knowledge-service'`

### 7.5 — API Layer

- `GET /kb/articles`, `POST /kb/articles`
- `GET /kb/articles/{id}`, `PUT /kb/articles/{id}`
- `POST /kb/articles/{id}/publish`, `POST /kb/articles/{id}/unpublish`
- `DELETE /kb/articles/{id}`
- `GET /kb/articles/{id}/versions`
- `GET /kb/categories`, `POST /kb/categories`
- `POST /kb/upload-url` → generate signed URL
- `POST /kb/upload-confirm` → confirm upload

---

## Phase 8 — Outbox + Inbox Pattern: Hoàn Chỉnh Cho Tất Cả .NET Services

> **Mục tiêu:** Outbox Pattern production-grade với SKIP LOCKED, backoff + jitter, DLQ, reclaim stale. Inbox Pattern dedup.
> **Tech tích hợp:** PostgreSQL `FOR UPDATE SKIP LOCKED`, RabbitMQ publisher confirms, exponential backoff

### 8.1 — OutboxPublisher (Shared, BuildingBlocks)

Hoàn thiện `IHostedService` OutboxPublisher dùng chung cho tất cả .NET services:

**Poll cycle (mỗi 500ms):**
1. `BEGIN TRANSACTION`
2. `SELECT * FROM ops.outbox_events WHERE service_name = :serviceName AND status IN ('pending','retry') AND next_retry_at <= NOW() ORDER BY occurred_at LIMIT 10 FOR UPDATE SKIP LOCKED`
3. Set `claimed_by = instanceId`, `claimed_at = NOW()`, `status = 'processing'`
4. `COMMIT`
5. Với mỗi row: publish RabbitMQ với publisher confirm (mandatory=true)
6. Nếu confirm OK: UPDATE `status='published'`, `published_at=NOW()`
7. Nếu fail: UPDATE `status='retry'`, `retry_count++`, `next_retry_at = NOW() + backoff(retry_count)`, `last_error = message`
8. Backoff formula: `min(2^retry_count * 1s + jitter(0-500ms), 300s)`
9. Sau 10 retries: UPDATE `status='dead'` → alert metric

**Reclaim stale claimed jobs:**
- Background task mỗi 1 phút
- `UPDATE ops.outbox_events SET status='pending', claimed_by=NULL, claimed_at=NULL WHERE status='processing' AND claimed_at < NOW() - INTERVAL '5 minutes'`

**Index cần thiết:**
- `CREATE INDEX idx_outbox_poll ON ops.outbox_events (service_name, status, next_retry_at) WHERE status IN ('pending','retry')`
- `CREATE INDEX idx_outbox_claimed_at ON ops.outbox_events (claimed_at) WHERE status = 'processing'`

### 8.2 — InboxConsumer (Shared, BuildingBlocks cho .NET services có PG)

Wrapper cho consumers nhận event từ RabbitMQ:

1. Extract `eventId` từ message headers
2. `INSERT INTO ops.inbox_messages (consumer_name, event_id, processed_at) VALUES (:name, :id, NOW()) ON CONFLICT (consumer_name, event_id) DO NOTHING`
3. Nếu rows_affected = 0 → đã xử lý → ACK message, return
4. Xử lý business logic
5. Nếu exception: NACK + requeue (exponential backoff tới max 5 lần), sau đó → DLQ

**Stuck processing cleanup:**
- Hangfire job `CleanupStuckInboxMessages`: mỗi 15 phút
- Xóa records trong `ops.inbox_messages` WHERE created_at > 7 ngày (giữ window replay an toàn)

### 8.3 — Verify Outbox End-to-End

- Tạo ticket → kiểm tra `ops.outbox_events` có row mới
- OutboxPublisher chạy → row chuyển sang `published`
- Kiểm tra RabbitMQ management UI: message xuất hiện trong queue
- Kiểm tra Mailpit UI: email notification đến

---

## Phase 9 — Search Service: ES Indexing + Hybrid Search

> **Mục tiêu:** Elasticsearch sync từ domain events. Unified search endpoint BM25 + kNN.
> **Tech tích hợp:** NestJS, `@elastic/elasticsearch`, BullMQ (embedding jobs), Redis (inbox dedup)

### 9.1 — Project Setup `search-service`

- Packages: `@elastic/elasticsearch`, `bullmq`, `ioredis`, `@nestjs/config`, OpenAI SDK (cho embedding)
- Config: ES endpoint + API key, OpenAI API key, Redis URI, RabbitMQ URI

### 9.2 — Elasticsearch Sync Consumers

> **Inbox dedup:** Mỗi consumer dùng Redis `SET inbox:search:{eventId} 1 NX EX 604800` (NX fail = skip).

**Consumer `support.ticket.created.v1`:**
- ES upsert ticket document với đầy đủ fields, `aggregate_version = event.version`

**Consumer `support.ticket.assigned.v1`:**
- ES update `agent_id`, `aggregate_version` (dùng Painless script để guard stale):
  ```
  if (ctx._source.aggregate_version < params.version) {
    ctx._source.agent_id = params.agent_id;
    ctx._source.aggregate_version = params.version;
  } else { ctx.op = 'none'; }
  ```

**Consumer `support.ticket.message-added.v1`:**
- Chỉ index nếu `is_internal_note = false`
- ES upsert `body_public_plain`, `aggregate_version`

**Consumer `support.ticket.resolved.v1`:**
- ES update `status = 'resolved'`, `resolved_at`, `aggregate_version`

**Consumer `support.ticket.closed.v1`:**
- ES update `status = 'closed'`, `closed_at`, `aggregate_version`

**Consumer `ai.summary.completed.v1`:**
- ES update `ai_summary`, `aggregate_version`

**Consumer `knowledge.article.published.v1`:**
- ES upsert article document (text fields)
- Enqueue BullMQ `embed-article` job (embedding tách riêng để không block text index)

**Consumer `knowledge.article.unpublished.v1`:**
- ES delete document

### 9.3 — Embedding Job (`embed-article`)

- BullMQ processor: fetch article body từ ES (hoặc từ payload)
- Call OpenAI `text-embedding-3-small` để generate embedding
- ES update `dense_vector` field của document
- Fallback: Ollama `nomic-embed-text` nếu OpenAI timeout

### 9.4 — Hybrid Search Endpoint

`GET /search?q=...&type=tickets|articles|all&page=1&size=20`

**Search logic:**
1. Generate query embedding (OpenAI)
2. ES query:
   ```json
   {
     "knn": { "field": "embedding", "query_vector": [...], "k": 5, "num_candidates": 50,
               "filter": { "term": { "tenant_id": "uuid" } } },
     "query": { "bool": { "must": { "multi_match": { "query": "...", "fields": ["title^2", "body_plain"] } },
                           "filter": { "term": { "tenant_id": "uuid" } } } },
     "size": 20
   }
   ```
3. Merge + rank kết quả, trả về unified response với `type`, `score`, `highlights`

### 9.5 — Analytics Cơ Bản

- Endpoint `GET /analytics/overview?tenantId=...&from=...&to=...` — trả về từ ES aggregation: tickets per status, per category, per day

---

## Phase 10 — AI Service: RAG + Classify + Assist + Memory

> **Mục tiêu:** Toàn bộ AI pipeline production-grade.
> **Tech tích hợp:** NestJS, LangChain.js, OpenAI SDK, Ollama fallback, Mongoose, BullMQ, Elasticsearch, circuit breaker

### 10.1 — Project Setup `ai-service`

- Packages: `langchain`, `@langchain/openai`, `@langchain/community`, `openai`, `mongoose`, `bullmq`, `ioredis`, `@elastic/elasticsearch`, `axios-retry`, `opossum` (circuit breaker)
- Config: OpenAI API key, Ollama endpoint, ES endpoint, MongoDB URI, Redis URI, RabbitMQ URI

### 10.2 — LLM Factory

- `LlmFactory`: trả `OpenAI GPT-4o-mini` làm primary, `Ollama llama3.2:3b` làm fallback
- Circuit breaker (`opossum`) wrap OpenAI HTTP client: threshold 50% error rate trong 10s → OPEN → dùng Ollama → sau 30s thử half-open
- Prometheus metric: `ai_circuit_breaker_state{provider="openai"}`

### 10.3 — RAG Pipeline (`POST /ai/ask`)

**Step 1 — Embed query:**
- Call OpenAI `text-embedding-3-small`
- Fallback: Ollama `nomic-embed-text`

**Step 2 — Hybrid Retrieve:**
- ES hybrid search (kNN + BM25) filter bắt buộc `tenant_id`
- Lấy top 3 chunks, giữ `_score` và `article_id`

**Step 3 — Augment Context:**
- Load `customer_memory` từ MongoDB (tenant_id, customer_email)
- Load session history từ Redis `ai_context:{sessionId}` TTL 30 phút

**Step 4 — Generate:**
- System prompt: "Answer ONLY based on provided KB articles. Cite source article IDs. Do not hallucinate."
- Temperature: 0.3
- Inject context: retrieved chunks + customer memory summary + recent history

**Step 5 — Calculate Confidence:**
```javascript
const SCORE_THRESHOLD = 0.7;
const maxScore = Math.max(...retrievedArticles.map(a => a._score));
const scoreNormalized = Math.min(maxScore / SCORE_THRESHOLD, 1.0);
const queryTerms = tokenize(query);
const matchedTerms = queryTerms.filter(t => topArticle.body_plain.includes(t));
const keywordOverlap = matchedTerms.length / queryTerms.length;
const confidence = (scoreNormalized * 0.7) + (keywordOverlap * 0.3);
```

**Step 6 — Route:**
- `confidence >= 0.75` → return answer + citations
- `confidence < 0.75` → return `{ escalate: true, reason: "low_confidence" }` → support-service tạo ticket

**Step 7 — Store:**
- MongoDB `chat_sessions`: append message + citations
- Redis `ai_context:{sessionId}`: update context (TTL 30 phút)
- MongoDB `ai_runs`: log model, tokens, latency, confidence, escalated
- Async: schedule `update-customer-memory` BullMQ job

### 10.4 — Auto-Classify Consumer (`support.ticket.created.v1`)

- Inbox dedup: MongoDB `inbox_messages` UNIQUE(consumer_name, event_id)
- Fetch ticket content từ support-service (internal HTTP)
- LLM call: classify category (billing/technical/general/...), priority (low/medium/high/urgent), sentiment (positive/neutral/negative), tags
- Publish `ai.classification.completed.v1` → support-service cập nhật ticket
- Log vào `ai_runs`
- Per-tenant rate limit: Redis counter 20 AI jobs/min/tenant (NACK + requeue nếu vượt)

### 10.5 — Ticket Summary Consumer (`support.ticket.resolved.v1`)

- Inbox dedup: MongoDB `inbox_messages`
- Fetch full ticket thread từ support-service
- LLM: tóm tắt conversation thành 2-3 câu
- Publish `ai.summary.completed.v1`
- Update `customer_memory`: MongoDB upsert (tenant_id, email) với topics, last_ticket_summary, sentiment
- Log `ai_runs`

### 10.6 — Agent Assist (`POST /ai/tickets/:id/suggest-reply`)

- Fetch ticket + messages từ support-service
- Fetch `customer_memory` từ MongoDB
- Search KB (ES hybrid) cho context liên quan
- LLM: draft reply phù hợp với tone (professional/friendly)
- Cache response: Redis `ai:suggest-reply:{ticketId}:{contentHash}` TTL 300s
- Log `ai_runs`

### 10.7 — Ticket Summary Endpoint (`GET /ai/tickets/:id/summary`)

- Fetch hoặc generate on-demand nếu chưa có
- Enqueue BullMQ job nếu generate async
- Return summary text (từ MongoDB `ai_runs` hoặc ticket.ai_summary)

### 10.8 — BullMQ Internal Jobs

- `ai-classify-job`: queue cho auto-classify (nếu dùng BullMQ thay vì direct consumer)
- `ai-summary-job`: queue cho ticket summary
- `update-customer-memory-job`: async update MongoDB customer_memory

### 10.9 — Backpressure

- RabbitMQ consumers: `basicQos(5)` — prefetch tối đa 5 messages cùng lúc
- Per-tenant rate limiter: Redis INCR `ai:ratelimit:{tenantId}:{minute}` → NACK + requeue nếu > 20

---

## Phase 11 — Real-time: WebSocket Chat + Presence + Collision Detection

> **Mục tiêu:** Live chat trên widget, typing indicator, presence, collision detection khi 2 agent cùng mở ticket.
> **Tech tích hợp:** Socket.io với Redis adapter, Lua distributed lock, MongoDB (chat sessions)

### 11.1 — WebSocket Events (Gateway BFF)

**Client → Server:**
- `join-ticket {ticketId}` → join room `ticket:{ticketId}`, acquire advisory lock
- `leave-ticket {ticketId}` → leave room, release lock
- `typing-start {ticketId}` → broadcast tới room
- `typing-stop {ticketId}` → broadcast tới room
- `send-message {ticketId, body}` → gọi support-service API, rồi broadcast

**Server → Client:**
- `ticket-updated {ticketId, changes}` → broadcast khi ticket thay đổi từ event
- `new-message {ticketId, message}` → broadcast khi có message mới
- `agent-editing {ticketId, agentId}` → broadcast collision warning
- `presence-update {userId, status}` → broadcast khi agent online/offline

### 11.2 — Presence Tracking

- Khi agent connect: `SET presence:user:{userId} 1 EX 30`
- Heartbeat mỗi 20s từ client → gia hạn TTL
- Khi disconnect: key tự expire → trigger keyspace event → broadcast offline

### 11.3 — Collision Detection (Advisory Lock)

Khi agent mở ticket editor → acquire advisory lock:
```lua
-- SET NX với token
local current = redis.call('GET', KEYS[1])
if current == false then
  redis.call('SET', KEYS[1], ARGV[1], 'PX', 30000)
  return 'acquired'
else
  return current  -- trả về token của owner hiện tại
end
```

- Lock key: `ticket:lock:{ticketId}`
- Lock value: `{agentId}:{instanceId}:{token}`
- Auto-renew mỗi 10s bằng Lua compare-and-renew (chỉ renew nếu token khớp)
- Release bằng Lua compare-and-delete (chỉ xóa nếu token khớp)
- Nếu lock đã có: broadcast `agent-editing` warning tới client mới join

### 11.4 — Chat Sessions (MongoDB)

- `chat_sessions`: lưu lịch sử chat widget per session
- Real-time relay: client → gateway WebSocket → support-service API (save PostgreSQL) → outbox event → notification-service → emit WebSocket event về tất cả agents đang xem ticket

---

## Phase 12 — Caching + Rate Limiting + Performance Tuning

> **Mục tiêu:** Redis cache-aside đầy đủ, cache stampede protection, PgBouncer tuning, k6 baseline.
> **Tech tích hợp:** Redis cache-aside, Lua mutex, Polly circuit breaker, PgBouncer, k6

### 12.1 — Redis Cache-Aside (Tất Cả .NET Services)

**Pattern cho mỗi cache:**
1. Check Redis `GET {key}`
2. Cache hit → deserialize, return
3. Cache miss → acquire Redis mutex `SET {key}:lock {instanceId} NX EX 5`
4. Double-check sau khi có lock (stampede prevention)
5. Load DB, serialize, `SET {key} {value} EX {ttl}`
6. Release lock `DEL {key}:lock`

**Cache entries:**
- `ticket:{tenantId}:{ticketId}` TTL 300s (invalidate khi ticket update)
- `kb:article:{tenantId}:{articleId}` TTL 3600s
- `user_permissions:{userId}:{tenantId}` TTL 3600s (invalidate khi role change)
- `feature_flags:{tenantId}` TTL 600s
- `sla_policy:{tenantId}:{policyId}` TTL 3600s
- `ai:suggest-reply:{ticketId}:{contentHash}` TTL 300s

**Cache invalidation inline khi write:**
- Mỗi update handler: `DEL` cache key trước khi COMMIT (hoặc sau COMMIT, chấp nhận 1 miss ngắn)

### 12.2 — Polly Circuit Breaker (.NET services → HTTP calls)

- Tất cả internal HTTP calls (identity↔workspace, support→workspace RBAC) dùng Polly:
  - Retry: 3× với backoff exponential
  - Circuit breaker: 50% error rate trong sliding window 10s → OPEN 30s
  - Timeout: 3s per request
- OpenAI HTTP client trong ai-service: tương tự nhưng dùng `opossum` (Node.js)

### 12.3 — PgBouncer Tuning

- `max_pool_size = 25` (tổng server connections ≤ 30)
- `pool_mode = transaction` (transaction pooling)
- Prometheus metrics: `pgbouncer_pool_stats` → Grafana panel "Pool wait time"
- Alert nếu `pool_wait_time p95 > 10ms`

### 12.4 — RabbitMQ Consumer Prefetch

- Mỗi consumer đặt `basicQos(prefetchCount: 5)` → không nhận quá 5 messages chưa ACK
- Đặc biệt ai-service: prefetchCount = 5, kết hợp per-tenant rate limiter

### 12.5 — k6 Baseline Load Test

Tạo `infra/k6/baseline.js`:
- 50 VUs, 3 phút
- Scenarios: login → get tickets list → get ticket detail → add message → search
- Collect p50/p95/p99 latency, throughput, error rate
- Run `EXPLAIN ANALYZE` trên top 3 slow queries từ PG log
- Thêm missing indexes dựa trên kết quả

---

## Phase 13 — Campaign Service: Follow-up Automation

> **Mục tiêu:** Campaign definitions, segment-based follow-up sau ticket resolved, throttled dispatch.
> **Tech tích hợp:** ASP.NET Core 8, EF Core 8, Hangfire (scheduled jobs), Redis advisory lock

### 13.1 — Project Setup `campaign-service`

- Packages: EF Core, MediatR, FluentValidation, Hangfire, StackExchange.Redis

### 13.2 — Domain Layer

- Entity `Campaign`: id, tenantId, name, segmentId, triggerEvent, messageTemplateId, delayMinutes, status
- Entity `CampaignDispatch`: id, campaignId, ticketId, customerId, dispatchedAt, status; UNIQUE(campaignId, ticketId) — idempotency guard

### 13.3 — Consumer `support.ticket.resolved.v1`

- Inbox dedup: `ops.inbox_messages`
- Load active campaigns cho tenant
- Check segment rules: customer match segment?
- Schedule Hangfire job `SendFollowUpJob` với delay `campaign.delayMinutes`

### 13.4 — Hangfire Job `SendFollowUpJob`

- Acquire Redis advisory lock: `SET campaign:lock:{campaignId}:{ticketId} {instanceId}:{token} NX PX 600000`
- Nếu lock không acquire được → skip (đang xử lý bởi instance khác)
- INSERT `campaign_dispatches` (UNIQUE guard — idempotency)
- Nếu duplicate → skip
- Publish RabbitMQ `campaign.dispatch.requested.v1` → notification-service gửi email
- Throttle: Hangfire job batch + sleep để không vượt 50 emails/s per tenant
- Release lock

---

## Phase 14 — Demo Data Seeding

> **Mục tiêu:** Dữ liệu giả lập đủ để demo và portfolio.

### 14.1 — Tạo 2 Demo Tenants

**Tenant 1: TaskFlow** (SaaS quản lý công việc)
- 1 Admin, 5 Agents, 2 Managers, 10 simulated customers
- 30 KB articles về product features, troubleshooting, billing
- 100 FAQ items
- 150 tickets với đầy đủ lifecycle (từ Open → Closed)
- 500+ ticket messages
- AI classifications + summaries đã gen

**Tenant 2: InvoiceFox** (SaaS billing/invoice)
- Cấu trúc tương tự
- KB articles về invoice, payment, tax, integrations

### 14.2 — Seeding Strategy

- Tạo `SeedDataService` (.NET console app hoặc migration seed)
- Seed thứ tự: tenants → users → memberships → KB categories → articles (publish từng cái để trigger event) → customers → tickets → messages → SLA data
- Dùng `Bogus` (.NET) hoặc `Faker.js` (NestJS) để gen dữ liệu realistic

### 14.3 — Seed AI Data

- Sau khi publish articles → ES indexing + embedding tự động qua events
- Tạo sẵn `ai_runs` documents cho các queries test
- Tạo `customer_memory` documents cho demo customers

---

## Phase 15 — Production Deploy: Docker Compose + Nginx + SSL

> **Mục tiêu:** Tất cả services chạy trên VPS thật, có HTTPS, health checks, resource limits.
> **Tech tích hợp:** Docker Compose (production mode), Nginx, Let's Encrypt (Certbot), named volumes, restart policies

### 15.1 — Docker Compose Production (`docker-compose.prod.yml`)

**Khác biệt so với dev:**
- Không mount source code, chỉ dùng built images
- Resource limits cho từng container: `mem_limit`, `cpus`
- `restart: unless-stopped` cho tất cả services
- Named volumes (không anonymous volumes)
- Healthcheck directive cho từng service
- Secrets qua environment variables (không hardcode)

**Tạo `.env.production`:**
- DB passwords, JWT keys, OpenAI API key, SMTP credentials
- Không commit `.env.production` vào git (thêm vào `.gitignore`)
- Store secrets trong GitHub Actions secrets hoặc VPS `/etc/signaldesk/.env`

### 15.2 — Nginx Config

Tạo `infra/nginx/nginx.conf`:

```nginx
# Reverse proxy mỗi subdomain về service tương ứng
# api.yourdomain.com → gateway-bff:3000
# Gzip compression
# SSL termination
# WebSocket upgrade headers cho /ws
# Rate limiting ở Nginx level (backup cho app-level)
# Security headers: HSTS, X-Frame-Options, Content-Security-Policy
```

**SSL với Let's Encrypt + Certbot:**
- Certbot container trong Docker Compose
- Auto-renew cron: `0 12 * * * docker compose run certbot renew`
- Nginx reload sau renew

### 15.3 — Service Dockerfiles

Tạo Dockerfile cho từng service:

**.NET services (multi-stage):**
```
Stage 1 (build): mcr.microsoft.com/dotnet/sdk:8.0
  - Restore, build, publish
Stage 2 (runtime): mcr.microsoft.com/dotnet/aspnet:8.0
  - Copy từ build stage
  - Non-root user
  - ENTRYPOINT
```

**NestJS services (multi-stage):**
```
Stage 1 (build): node:20-alpine
  - npm ci, npm run build
Stage 2 (runtime): node:20-alpine
  - npm ci --production
  - Copy dist/
  - Non-root user
  - CMD node dist/main
```

### 15.4 — Database Migration Strategy (Production)

- .NET services: EF Core migrations chạy tự động khi startup (`context.Database.MigrateAsync()`)
- Hoặc tạo separate migration job trong Docker Compose `depends_on`
- Không chạy migration trong development Docker container — chỉ trong production deploy

### 15.5 — Production Startup Order

Docker Compose `depends_on` + `condition: service_healthy`:
```
postgres → pgbouncer
mongodb
redis
rabbitmq
elasticsearch
supabase-storage (external)
↓
identity-service (depends: postgres, redis, rabbitmq)
workspace-service (depends: postgres, redis, rabbitmq)
↓
support-service (depends: postgres, redis, rabbitmq)
knowledge-service (depends: postgres, redis, rabbitmq, supabase-storage external)
notification-service (depends: mongodb, redis, rabbitmq)
search-service (depends: elasticsearch, redis, rabbitmq)
ai-service (depends: elasticsearch, mongodb, redis, rabbitmq)
campaign-service (depends: postgres, redis, rabbitmq)
↓
gateway-bff (depends: tất cả services)
nginx (depends: gateway-bff)
```

---

## Phase 16 — GitHub Actions CI/CD: Full Pipeline

> **Mục tiêu:** CI build + test + deploy tự động khi push vào main.
> **Tech tích hợp:** GitHub Actions, Docker Hub hoặc GHCR, SSH deploy

### 16.1 — CI Pipeline Hoàn Chỉnh (`.github/workflows/ci.yml`)

```
on: push (develop, feature/*), PR (main)

jobs:
  lint:
    - dotnet format --verify-no-changes
    - nx run-many --target=lint

  test:
    - dotnet test --collect:"XPlat Code Coverage"
    - nx run-many --target=test
    - Coverage threshold gate: < 70% → fail

  build:
    - dotnet build --configuration Release
    - nx run-many --target=build
    - Docker build (không push, chỉ verify image builds)

  openapi-check:
    - Generate OpenAPI spec từ mỗi service
    - Diff với spec committed trong repo
    - Fail nếu có breaking changes
```

### 16.2 — CD Pipeline (`.github/workflows/deploy-production.yml`)

```
on: push (main)

jobs:
  build-and-push:
    - Docker build mỗi service image
    - Push lên GHCR (ghcr.io/{user}/signaldesk/{service}:{sha})
    - Tag: latest + git SHA

  deploy:
    - SSH vào VPS
    - Pull images mới từ GHCR
    - docker compose -f docker-compose.prod.yml up -d --no-deps {service}
      (rolling update từng service, không down tất cả cùng lúc)
    - Health check: curl GET /health/ready của từng service sau deploy
    - Rollback: nếu health check fail → docker compose up -d với image SHA cũ
```

### 16.3 — Secrets Management

GitHub Actions Secrets:
- `VPS_HOST`, `VPS_USER`, `VPS_KEY` (SSH private key)
- `GHCR_TOKEN`
- `OPENAI_API_KEY`
- `JWT_PRIVATE_KEY`
- `POSTGRES_PASSWORD`, `MONGODB_URI`, `REDIS_PASSWORD`

---

## Phase 17 — Observability: Prometheus + Grafana + Jaeger + Loki

> **Mục tiêu:** Full observability stack. Distributed traces, metrics dashboards, log aggregation, alerts.
> **Tech tích hợp:** OpenTelemetry (.NET + NestJS), Jaeger/Tempo, Prometheus, Grafana, Loki + Promtail, Sentry, UptimeRobot

### 17.1 — OpenTelemetry Instrumentation

**.NET services:**
- Thêm packages: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.Otlp`
- Configure trong `Program.cs`:
  - Trace: instrument ASP.NET Core + HTTP client + EF Core → export OTLP → Jaeger
  - Metric: instrument ASP.NET Core → export Prometheus scrape endpoint `/metrics`
  - Enrich spans với: `tenant_id`, `correlation_id`, `service.name`

**NestJS services:**
- Package: `@opentelemetry/sdk-node`, `@opentelemetry/auto-instrumentations-node`, `@opentelemetry/exporter-jaeger`, `@opentelemetry/exporter-prometheus`
- Khởi tạo trước `NestFactory.create` trong `main.ts`
- Enrich spans: tenant_id, correlation_id

**Propagation:** W3C TraceContext headers (`traceparent`) forward qua gateway → downstream services → bắt buộc log `trace_id` trong mọi log line

### 17.2 — Prometheus Metrics

**Metrics cần expose từ mỗi service:**
- `http_requests_total{method, route, status}` — counter
- `http_request_duration_seconds{method, route, status}` — histogram (buckets: 0.01, 0.05, 0.1, 0.2, 0.5, 1, 3)
- `.NET services:` `dotnet_runtime_memory_bytes`, `dotnet_gc_collection_total`
- `support-service:` `tickets_created_total{tenantId}`, `sla_breached_total{tenantId}`
- `ai-service:` `ai_inference_duration_seconds{type}`, `ai_confidence_score{type}`, `ai_escalation_total`
- `notification-service:` `emails_sent_total`, `email_dlq_count`
- `search-service:` `es_index_duration_seconds`, `search_request_duration_seconds`
- `gateway:` `rate_limit_rejected_total{tenantId, route}`, `websocket_connections_active`

**`docker-compose.infra.yml` — bật observability services:**
```yaml
prometheus:
  image: prom/prometheus:latest
  volumes:
    - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
  scrape configs: tất cả services /metrics endpoint

grafana:
  image: grafana/grafana:latest
  volumes:
    - grafana-data:/var/lib/grafana
    - ./infra/grafana/dashboards:/etc/grafana/provisioning/dashboards

jaeger:
  image: jaegertracing/all-in-one:latest
  ports: 16686 (UI), 4317 (OTLP gRPC)

loki:
  image: grafana/loki:latest

promtail:
  image: grafana/promtail:latest
  volumes:
    - /var/lib/docker/containers:/var/lib/docker/containers:ro
    - ./infra/promtail/config.yml:/etc/promtail/config.yml
```

### 17.3 — Grafana Dashboards (Provisioning)

Tạo `infra/grafana/dashboards/` với 3 dashboards JSON:

**Dashboard 1: API Health**
- Request rate per service (RPS) — time series
- p50/p95/p99 latency per service — time series, alert rule `p95 > 200ms`
- Error rate 4xx/5xx per service — heatmap
- SLO burn rate (1h/6h/24h/3d) — gauge
- HTTP 409 Conflict rate — counter
- Active WebSocket connections — gauge

**Dashboard 2: Queue Health**
- RabbitMQ queue depth per queue (notification, ai-tasks, search-index, campaigns, DLQ)
- Oldest ready message age per queue, alert `> 30s`
- Consumer throughput (messages/s)
- DLQ message count, alert nếu `> 0`
- Outbox pending events count, alert nếu `> 50` quá 5 phút
- Backpressure trigger count per tenant

**Dashboard 3: AI Pipeline**
- ai-service RPS tới LLM provider
- AI response p95 latency per type (classify/rag/summarize)
- Circuit breaker state (OPEN/CLOSED/HALF-OPEN), alert OPEN > 30s
- AI confidence score avg (7-day rolling), alert < 0.70
- Escalation rate (confidence < 0.75)
- ai_runs count per type per day
- Fallback to Ollama rate

### 17.4 — Sentry Integration

- Cài Sentry SDK cho từng .NET service: `Sentry.AspNetCore`
- Cài Sentry SDK cho NestJS services: `@sentry/node`
- Configure DSN từ environment variable
- Enrich events với: `tenant_id`, `user_id`, `correlation_id`
- Alert rules trong Sentry: new error → Slack/email notify

### 17.5 — UptimeRobot

- Tạo monitors cho từng public endpoint:
  - `GET https://api.yourdomain.com/api/health` — aggregate health check
  - `GET https://api.yourdomain.com/api/auth/me` (expect 401, không 502)
- Alert email khi downtime > 1 phút
- Public status page (optional)
- Target: 99.5% uptime over 30 ngày

### 17.6 — Alert Rules (Prometheus + Grafana)

```yaml
groups:
  - name: signaldesk
    rules:
      - alert: HighApiLatency
        expr: histogram_quantile(0.95, http_request_duration_seconds) > 0.2
        for: 5m
      - alert: DlqNotEmpty
        expr: rabbitmq_queue_messages{queue=~".*dlq"} > 0
        for: 1m
      - alert: AiCircuitBreakerOpen
        expr: ai_circuit_breaker_state{state="open"} == 1
        for: 30s
      - alert: OutboxPendingHigh
        expr: outbox_pending_events > 50
        for: 5m
      - alert: PgBouncerPoolWaitHigh
        expr: pgbouncer_pool_wait_seconds_p95 > 0.01
        for: 5m
```

---

## Phase 18 — Testing: Unit + Integration + Load Test Final

> **Mục tiêu:** Test coverage đủ, integration tests xuyên services, load test final documented.
> **Tech tích hợp:** xUnit, Jest, Testcontainers, Pact (contract), k6

### 18.1 — Unit Tests Hoàn Chỉnh

**.NET (xUnit):**
- `support-service`: ticket state machine, SLA calculation, optimistic lock (409), idempotency
- `identity-service`: JWT generation/validation, BCrypt, refresh token rotation
- `workspace-service`: RBAC permission lookup, cache invalidation
- `knowledge-service`: versioning, slug unique validation
- Coverage threshold: ≥ 70% per service

**NestJS (Jest):**
- `ai-service`: confidence calculation, circuit breaker behavior, Inbox dedup
- `search-service`: stale event guard logic, hybrid search query building
- `notification-service`: Redis inbox dedup, null agent guard, dedupe_key logic

### 18.2 — Integration Tests (Testcontainers)

Tạo `tests/integration/` với `docker-compose.test.yml` chạy real dependencies:

**Test scenarios:**
- `CreateTicket → OutboxPublisher → RabbitMQ → SearchService ES upsert`
- `AddMessage idempotency: gửi 2 lần cùng Idempotency-Key → chỉ 1 message được tạo`
- `MemberRemoved → identity-service revoke refresh tokens → refresh token rejected`
- `ArticlePublish → embedding job → ES dense_vector upsert → RAG query trả về article`
- `TicketCreate → ai-service classify → support-service update ticket fields`
- `Optimistic lock: 2 concurrent AssignTicket cùng expectedVersion → 1 thành công, 1 409`

**Chạy trong CI:**
```yaml
- name: Integration Tests
  run: docker compose -f docker-compose.test.yml up --abort-on-container-exit
```

### 18.3 — Contract Tests (Pact)

- `gateway-bff → support-service`: HTTP contract cho `POST /tickets`, `GET /tickets/{id}`
- `identity-service → workspace-service`: HTTP contract cho `/internal/memberships`
- RabbitMQ event schema validation: JSON Schema cho mỗi event type trong `libs/contracts/events`

### 18.4 — k6 Final Load Test

Tạo `infra/k6/final-load-test.js`:
- 100 VUs, 5 phút
- Scenarios mix: 40% get tickets, 20% search, 20% get ticket detail, 10% add message, 10% AI ask
- SLO targets:
  - p95 latency < 200ms
  - p99 latency < 500ms
  - Throughput > 200 req/min
  - Error rate < 1%
- Generate HTML report: `k6 run --out json=results.json final-load-test.js`
- Document kết quả trong README hoặc `docs/load-test-results.md`

---

## Phase 19 — API Documentation + OpenAPI

> **Mục tiêu:** Swagger/OpenAPI tự động từ annotations, unified docs tại Gateway.

### 19.1 — .NET Services OpenAPI

- Thêm `Swashbuckle.AspNetCore` vào mỗi .NET service
- Annotations cho mọi endpoint: `[ProducesResponseType]`, `[SwaggerOperation]`
- Authentication scheme: Bearer JWT
- Publish tại `/swagger/v1/swagger.json`

### 19.2 — NestJS Services OpenAPI

- `@nestjs/swagger` decorators: `@ApiOperation`, `@ApiResponse`, `@ApiBearerAuth`
- Generate spec tại `/swagger.json`

### 19.3 — Gateway Unified Docs

- Gateway gom docs từ tất cả services vào `/api/docs`
- Dùng Swagger UI hoặc Redoc
- Internal gRPC docs từ `.proto` files (nếu có)
- Event contract docs từ JSON Schema trong `libs/contracts/events`

---

## Phase 20 — Final Polish + Demo Scenarios

> **Mục tiêu:** Project sẵn sàng demo và portfolio.

### 20.1 — README.md

Viết README đầy đủ:
- Architecture diagram (link tới file trong repo)
- Tech stack bảng với lý do chọn
- Local development setup (step by step)
- Production URL live
- Load test kết quả
- Demo tenant credentials
- Link Grafana dashboard (nếu public)
- Link Swagger docs

### 20.2 — Demo Scenarios Verification

Verify 4 demo scenarios hoạt động:

**Scenario A — RAG Chatbot không hallucinate:**
- Customer hỏi câu có trong KB → bot trả lời + citation
- Customer hỏi câu ngoài KB → bot nói "không đủ tự tin" + create ticket
- Jaeger trace: widget → gateway → chat-session → ai-service → ES → LLM hiện rõ

**Scenario B — Event-driven pipeline sống:**
- Tạo ticket mới → RabbitMQ UI: message xuất hiện → Mailpit: email đến → ticket có AI classification
- Grafana: queue depth = 0, oldest message age < 1s

**Scenario C — Tenant isolation:**
- Login TaskFlow admin → thấy tickets TaskFlow
- Login InvoiceFox admin → thấy tickets InvoiceFox
- Dùng JWT TaskFlow, truyền tenantId InvoiceFox → Gateway reject 403

**Scenario D — Observability stack:**
- Grafana: latency p50/p95/p99, throughput, error rate
- Jaeger: distributed trace xuyên services
- Loki: log search theo `correlation_id`
- k6 report: p95 < 200ms tại 100 VUs

### 20.3 — Checklist Trước Khi Gọi Done

- [ ] Tất cả services có `GET /health/ready` trả 200
- [ ] `GET /api/health` aggregate trả healthy
- [ ] Swagger docs accessible tại `/api/docs`
- [ ] Grafana dashboards load không lỗi
- [ ] Jaeger: có ít nhất 1 trace xuyên 3+ services
- [ ] UptimeRobot: monitor xanh
- [ ] Demo tenant data seeded đầy đủ
- [ ] k6 final report documented
- [ ] README đầy đủ với live URL
- [ ] GitHub Actions CI/CD xanh hết
- [ ] DLQ count = 0 sau demo runs
- [ ] Outbox pending count = 0 sau vài phút

---

## Phụ Lục A — Tech Stack Integration Summary

| Phase | Services | Tech Mới Tích Hợp |
|-------|----------|-------------------|
| 0 | Tất cả | NX monorepo, Docker Compose infra |
| 1 | Infra | PostgreSQL, PgBouncer, MongoDB, Redis, RabbitMQ, Elasticsearch, Mailpit, Ollama, Supabase Storage external |
| 2 | identity-service | EF Core 8, MediatR, FluentValidation, BCrypt, JWT RS256, Hangfire |
| 3 | workspace-service | Redis cache (permissions), internal HTTP calls |
| 4 | gateway-bff | NestJS JWT guard, Socket.io, Redis rate limit |
| 5 | support-service | CQRS pattern, optimistic locking, FOR UPDATE ticket_no |
| 6 | notification-service | BullMQ, Nodemailer, Handlebars, Redis inbox dedup |
| 7 | knowledge-service | Supabase Storage signed URL, versioning pattern |
| 8 | Tất cả .NET | Outbox SKIP LOCKED, Inbox dedup, exponential backoff |
| 9 | search-service | ES hybrid search (BM25+kNN), embedding jobs, Painless script |
| 10 | ai-service | LangChain.js, OpenAI SDK, Ollama, confidence scoring, circuit breaker |
| 11 | gateway-bff | Socket.io Redis adapter, Lua distributed lock, presence |
| 12 | Tất cả | Redis cache-aside + stampede protection, Polly, k6 |
| 13 | campaign-service | Hangfire scheduled jobs, Redis advisory lock |
| 14 | Seeding | Bogus / Faker.js |
| 15 | Infra | Docker Compose production, Nginx, Certbot SSL |
| 16 | CI/CD | GitHub Actions full pipeline, GHCR |
| 17 | Observability | OpenTelemetry, Jaeger, Prometheus, Grafana, Loki, Sentry, UptimeRobot |
| 18 | Tests | xUnit, Jest, Testcontainers, Pact, k6 final |
| 19 | Docs | Swashbuckle, @nestjs/swagger, unified OpenAPI |
| 20 | Final | Demo verification, README |

---

## Phụ Lục B — Event Contracts Quick Reference

| Event | Producer | Consumers |
|-------|----------|-----------|
| `identity.user.registered.v1` | identity-service | workspace-service, notification-service |
| `identity.user.email-verified.v1` | identity-service | notification-service |
| `workspace.tenant.created.v1` | workspace-service | notification-service |
| `workspace.member.invited.v1` | workspace-service | notification-service |
| `workspace.member.removed.v1` | workspace-service | identity-service |
| `support.ticket.created.v1` | support-service | notification, search, ai-service |
| `support.ticket.assigned.v1` | support-service | notification, search |
| `support.ticket.message-added.v1` | support-service | notification, search |
| `support.ticket.resolved.v1` | support-service | notification, search, ai-service, campaign |
| `support.ticket.closed.v1` | support-service | search |
| `support.ticket.sla-breached.v1` | support-service | notification |
| `knowledge.article.published.v1` | knowledge-service | search-service |
| `knowledge.article.unpublished.v1` | knowledge-service | search-service |
| `ai.classification.completed.v1` | ai-service | support-service |
| `ai.summary.completed.v1` | ai-service | support-service, search-service |
| `campaign.dispatch.requested.v1` | campaign-service | notification-service |

---

## Phụ Lục C — Inbox Dedup Strategy Per Service

| Service | Dedup Store | Key / Method |
|---------|------------|-------------|
| support-service | PostgreSQL `ops.inbox_messages` | UNIQUE(consumer_name, event_id) |
| identity-service | PostgreSQL `ops.inbox_messages` | UNIQUE(consumer_name, event_id) |
| workspace-service | PostgreSQL `ops.inbox_messages` | UNIQUE(consumer_name, event_id) |
| campaign-service | PostgreSQL `ops.inbox_messages` | UNIQUE(consumer_name, event_id) |
| notification-service | Redis NX | `SET inbox:notification:{eventId} 1 NX EX 604800` |
| search-service | Redis NX | `SET inbox:search:{eventId} 1 NX EX 604800` |
| ai-service | MongoDB UNIQUE | `db.inbox_messages.insertOne({ consumer_name, event_id })` — duplicate key = skip |

---

*Cập nhật `Trạng Thái` khi hoàn thành mỗi Phase:*
```
Phase hoàn thành: [ ]
Phase đang làm:   [ ]
Vấn đề gặp phải: [ ]
```

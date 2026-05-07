# SignalDesk AI — Backend Timeline (2–4 Tuần)
> **Dành riêng cho BE.** FE chạy song song ở agent khác.  
> Mỗi tuần = 5–7 ngày làm việc. Nếu làm full-time, có thể hoàn thành trong 2 tuần.  
> Nếu làm part-time (4–5h/ngày), target 3–4 tuần.

---

## Quy Ước Ký Hiệu

| Ký hiệu | Ý nghĩa |
|---------|---------|
| 🏗️ | Setup / Scaffold |
| ⚙️ | Config / Infra |
| 🧩 | Implement feature/module |
| 🔗 | Tích hợp tech/service |
| ✅ | Checkpoint / Có thể demo |
| ⚠️ | Critical path — không được bỏ qua |
| 🎯 | Deploy / Production step |

---

## TUẦN 1 — Foundation, Identity, Workspace, Gateway

> **Mục tiêu cuối tuần:** Auth hoàn chỉnh (register → login → JWT → refresh → RBAC), gateway proxy, infra local chạy ổn định.

---

### Ngày 1 — Repo & Infrastructure Setup

#### 1.1 🏗️ Khởi tạo NX Monorepo
- Tạo NX workspace với preset `apps` + `libs`
- Cấu trúc thư mục:
  ```
  apps/
    gateway-bff/         ← NestJS
    identity-service/    ← ASP.NET Core 8
    workspace-service/   ← ASP.NET Core 8
    support-service/     ← ASP.NET Core 8
    knowledge-service/   ← ASP.NET Core 8
    notification-service/← NestJS
    search-service/      ← NestJS
    ai-service/          ← NestJS
    campaign-service/    ← ASP.NET Core 8 (tuần 4)
  libs/
    contracts/
      events/            ← JSON Schema event contracts
      grpc/              ← .proto files (nếu cần)
      http/              ← internal API types
    building-blocks/
      dotnet/            ← shared C# packages
      nestjs/            ← shared NestJS modules
  ```
- Thêm `.gitignore` cho Node, .NET, Docker
- Khởi tạo Git repo + đẩy lên GitHub

#### 1.2 ⚙️ `docker-compose.infra.yml` — Infrastructure Local
Tạo file `docker-compose.infra.yml` với các services sau (theo đúng port trong spec):

| Service | Image | Port |
|---------|-------|------|
| PostgreSQL 16 | `postgres:16-alpine` | 5432 |
| PgBouncer | `edoburu/pgbouncer` | 6432 |
| MongoDB 7 | `mongo:7` | 27017 |
| Redis 7 | `redis:7-alpine` | 6379 |
| RabbitMQ 3.13 | `rabbitmq:3.13-management` | 5672, 15672 |
| Elasticsearch 8 | `elasticsearch:8.x` | 9200 |
| Supabase Storage | external managed service | signed URL upload/download |
| Mailpit | `axllent/mailpit` | 1025, 8025 |
| Ollama | `ollama/ollama` | 11434 |

- Thêm `healthcheck` cho mỗi service
- Thêm named volumes để persist data
- Tạo script `scripts/infra-up.sh` và `scripts/infra-down.sh`
- Chạy thử: `docker compose -f docker-compose.infra.yml up -d`

#### 1.3 ⚙️ PostgreSQL Schemas
Kết nối trực tiếp vào PostgreSQL (không qua PgBouncer) và tạo schemas:
```
identity / workspace / support / knowledge / campaign / ops
```
- Tạo database user riêng cho từng service với permission chỉ vào schema của mình
- Tạo `ops` schema với quyền write cho tất cả services

#### 1.4 ⚙️ PgBouncer Config
- Cấu hình `pgbouncer.ini`: `pool_mode = transaction`, `max_client_conn = 100`, `default_pool_size = 25`
- Verify: tất cả services sẽ connect qua port `6432` (PgBouncer), không phải `5432` (PostgreSQL trực tiếp)

#### 1.5 ⚙️ RabbitMQ Exchanges & Queues
Tạo `scripts/rabbitmq-setup.sh` (chạy qua RabbitMQ CLI hoặc Management API) để khởi tạo:

**Exchanges (type: `topic`):**
- `signaldesk.events` — main domain events
- `signaldesk.dlx` — dead letter exchange

**Queues + Bindings:**
| Queue | Binding key | DLQ |
|-------|-------------|-----|
| `notifications` | `#.created.#`, `#.assigned.#`, `#.resolved.#`, `#.sla-breached.#` | `notifications.dlq` |
| `search-index` | `support.ticket.#`, `knowledge.article.#` | `search-index.dlq` |
| `ai-tasks` | `support.ticket.created.v1`, `support.ticket.resolved.v1` | `ai-tasks.dlq` |
| `campaigns` | `support.ticket.resolved.v1` | `campaigns.dlq` |
| `identity-events` | `workspace.member.removed.v1` | `identity-events.dlq` |
| `workspace-events` | `identity.user.registered.v1` | `workspace-events.dlq` |

- Set `x-dead-letter-exchange: signaldesk.dlx` cho mỗi queue
- Verify trên RabbitMQ UI: `http://localhost:15672`

#### 1.6 ⚙️ Elasticsearch Index Template
Tạo index template `kb_articles_v1` với mapping:
- Fields: `tenant_id`, `article_id`, `title`, `body_plain`, `slug`, `tags`, `status`, `published_at`, `aggregate_version`
- Dense vector field `embedding` (dims: 1536) cho kNN

Tạo index template `tickets_v1` với mapping:
- Fields: `tenant_id`, `ticket_id`, `ticket_no`, `subject`, `status`, `category`, `priority`, `customer_email`, `assigned_agent_id`, `created_at`, `aggregate_version`

#### 1.7 🏗️ .NET BuildingBlocks Library
Tạo `libs/building-blocks/dotnet/` với các packages:
- `BuildingBlocks.Domain` — base classes: `Entity<TId>`, `AggregateRoot<TId>`, `DomainEvent`, `ValueObject`, `IDomainEventDispatcher`
- `BuildingBlocks.Application` — interfaces: `ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`, `IUnitOfWork`, `ITenantContext`, `ICurrentUser`
- `BuildingBlocks.Infrastructure` — shared: `OutboxRepository`, `InboxRepository`, `AuditLogRepository`, `TenantMiddleware`, `CorrelationIdMiddleware`
- `BuildingBlocks.Messaging` — `OutboxEvent` entity, `InboxMessage` entity, `IOutboxPublisher`, `IMessageConsumer`

#### 1.8 🏗️ NestJS Common Modules
Tạo `libs/building-blocks/nestjs/` với:
- `TenantModule` — extract `X-Tenant-Id` từ header, inject vào request context
- `CorrelationModule` — generate/forward `X-Correlation-Id`
- `LoggingModule` — Winston logger config (JSON format, include `tenantId`, `correlationId`, `service`)
- `HealthModule` — base health check (liveness + readiness)
- `RabbitMQModule` — wrapper cho `@golevelup/nestjs-rabbitmq` với error handling

#### 1.9 🔗 GitHub Actions CI Pipeline
Tạo `.github/workflows/ci.yml`:
- Trigger: `push` to `main`, `pull_request`
- Jobs: `build-dotnet`, `build-nestjs`, `lint`, `test`
- Mỗi job chạy độc lập, fail fast
- Cache: `.npm`, `~/.nuget/packages`

✅ **Checkpoint Ngày 1:** `docker compose up` chạy được toàn bộ infra, PostgreSQL có schemas, RabbitMQ có exchanges/queues, Elasticsearch có index templates, CI pipeline màu xanh.

---

### Ngày 2 — Identity Service

#### 2.1 🏗️ Scaffold `identity-service` (ASP.NET Core 8)
- Tạo project với cấu trúc Clean Architecture:
  ```
  IdentityService/
    Domain/
      Entities/      ← User, RefreshToken
      Events/        ← UserRegistered, EmailVerified
      Exceptions/
    Application/
      Commands/      ← RegisterUser, LoginUser, RefreshToken, VerifyEmail
      Queries/
      DTOs/
    Infrastructure/
      Persistence/   ← IdentityDbContext, Migrations
      Repositories/
      Messaging/     ← OutboxPublisher (hosted service)
      BackgroundJobs/← RefreshTokenCleanup (Hangfire)
    API/
      Controllers/   ← AuthController
      Middleware/
      Program.cs
  ```
- Cài packages: `MediatR`, `FluentValidation`, `EF Core 8`, `BCrypt.Net`, `Microsoft.IdentityModel.Tokens`, `Hangfire`
- Kết nối `BuildingBlocks.Domain`, `BuildingBlocks.Infrastructure`

#### 2.2 ⚙️ Database Schema — `identity` 
Tạo EF Core migrations cho:
- `identity.users`: `id` (uuid), `email` (unique), `password_hash`, `email_verified_at`, `created_at`, `updated_at`
- `identity.refresh_tokens`: `id`, `user_id`, `token_hash` (indexed), `tenant_id`, `expires_at`, `revoked_at`, `created_at`
- `ops.outbox_events` (shared schema, tạo nếu chưa có): `id`, `service_name`, `aggregate_type`, `aggregate_id`, `event_type`, `payload` (jsonb), `headers` (jsonb), `status` (pending/published/failed), `retry_count`, `next_retry_at`, `claimed_by`, `claimed_at`, `last_error`, `occurred_at`, `published_at`
- `ops.inbox_messages`: `id`, `consumer_name`, `event_id`, `event_type`, `payload`, `status` (processing/processed/failed), `processed_at`, `created_at`
- Thêm index: `outbox_events(service_name, status, next_retry_at)`, `outbox_events(claimed_at)`, `inbox_messages(consumer_name, event_id)` UNIQUE

#### 2.3 🧩 Domain — User & RefreshToken Entities
- `User` aggregate: `Register()` static factory, `VerifyEmail()`, `ChangePassword()` methods
- `Register()` raises `UserRegisteredDomainEvent`
- `VerifyEmail()` raises `EmailVerifiedDomainEvent`
- `RefreshToken` value object: rotation logic (tạo mới + revoke cũ cùng lúc)

#### 2.4 🧩 Application Layer — Commands & Handlers
- `RegisterUserCommand` → `RegisterUserHandler`: hash password (BCrypt cost 12), save user, add outbox event `identity.user.registered.v1`, commit UoW
- `LoginUserCommand` → `LoginUserHandler`: verify password, generate JWT (RS256, 15min), generate refresh token (hashed, 30 days), save refresh token
- `RefreshTokenCommand` → `RefreshTokenHandler`:
  - Validate refresh token (lookup by hash, check expiry, check revoked)
  - ⚠️ Sync call tới `workspace-service /internal/memberships?userId=X&tenantId=Y` để kiểm tra membership còn Active không (Bug fix v6.0)
  - Nếu membership removed → revoke refresh token, return 401
  - Token rotation: revoke old, create new
- `VerifyEmailCommand` → `VerifyEmailHandler`: mark verified, add outbox event `identity.user.email-verified.v1`
- FluentValidation cho mỗi command

#### 2.5 🔗 JWT Configuration
- Dùng RS256 (asymmetric key pair): private key cho sign, public key expose qua `/.well-known/jwks.json` (gateway dùng để verify)
- JWT claims: `sub` (userId), `email`, `tenantId`, `role`, `exp`
- Access token: 15 phút, Refresh token: 30 ngày (stored hashed trong DB)

#### 2.6 🧩 OutboxPublisher — IHostedService
- Poll `ops.outbox_events WHERE service_name='identity-service' AND status='pending' AND next_retry_at <= NOW()` mỗi 500ms
- Claim batch bằng `FOR UPDATE SKIP LOCKED` (batch size: 10)
- Publish lên RabbitMQ với broker confirm
- Nếu confirm OK → mark `published`
- Nếu fail → exponential backoff `5s → 15s → 45s → 2m → 5m`, sau max retry → mark `failed` (DLQ)
- Reclaim stale locks: cron job scan `claimed_at < NOW() - 2min AND status='pending'` → reset `claimed_by = NULL`

#### 2.7 🧩 RabbitMQ Consumer — `workspace.member.removed.v1`
- Inbox dedup: `INSERT ops.inbox_messages (consumer_name='identity-member-removed', event_id)` — duplicate key → skip
- Khi member removed: `UPDATE identity.refresh_tokens SET revoked_at = NOW() WHERE user_id = :userId AND tenant_id = :tenantId AND revoked_at IS NULL`

#### 2.8 🧩 Hangfire Jobs
- `RefreshTokenCleanup`: mỗi 6h, xóa `identity.refresh_tokens WHERE expires_at < NOW() - 7 days`

#### 2.9 ⚙️ Health Endpoints
- `GET /health/live` — process alive
- `GET /health/ready` — PostgreSQL + RabbitMQ reachable, migrations applied

✅ **Checkpoint Ngày 2:** `POST /auth/register` → `POST /auth/login` trả JWT + refresh token, `POST /auth/refresh` hoạt động với membership check, OutboxPublisher tự publish events lên RabbitMQ.

---

### Ngày 3 — Workspace Service & Gateway BFF

#### 3.1 🏗️ Scaffold `workspace-service` (ASP.NET Core 8)
Cấu trúc tương tự identity-service, với:
- Domain: `Tenant`, `Membership`, `Role`, `Permission`, `FeatureFlag`
- Application: Commands cho `CreateTenant`, `InviteMember`, `RemoveMember`, `AssignRole`, `UpdateFeatureFlag`
- Infrastructure: `WorkspaceDbContext`, Outbox, Redis client (cho permission cache + feature flag cache)

#### 3.2 ⚙️ Database Schema — `workspace`
Migrations cho:
- `workspace.tenants`: `id`, `name`, `slug` (unique), `plan` (Starter/Pro/Enterprise), `status`, `created_at`
- `workspace.memberships`: `id`, `tenant_id`, `user_id`, `role_id`, `status` (Active/Suspended/Removed), `invited_by`, `joined_at`
- `workspace.roles`: `id`, `tenant_id`, `name` (Admin/Manager/Agent/Analyst), `is_system`
- `workspace.permissions`: `id`, `resource`, `action` (enum)
- `workspace.role_permissions`: `role_id`, `permission_id` (composite PK)
- `workspace.feature_flags`: `id`, `tenant_id`, `flag_key`, `enabled`, `rollout_percentage`, `updated_at`
- `workspace.teams`: `id`, `tenant_id`, `name`, `created_at`
- `workspace.team_members`: `team_id`, `user_id`

#### 3.3 🧩 RBAC Logic
- Seed mặc định 4 roles: `Admin`, `Manager`, `Agent`, `Analyst` với permissions tương ứng
- Cache permissions: `user_permissions:{userId}:{tenantId}` trong Redis TTL 3600s
- Invalidate cache khi role hoặc membership thay đổi
- Feature flags: `feature_flags:{tenantId}` cached TTL 600s

#### 3.4 🧩 Event Consumers & Producers
- **Consumer** `identity.user.registered.v1`: inbox dedup → tạo Starter tenant → tạo Admin membership → commit → add outbox `workspace.tenant.created.v1` + `workspace.member.joined.v1`
- **Producer** `workspace.tenant.created.v1` → notification-service (welcome email)
- **Producer** `workspace.member.invited.v1` → notification-service (invite email)
- **Producer** `workspace.member.removed.v1` → identity-service (revoke tokens)

#### 3.5 🧩 Internal HTTP Endpoint
- `GET /internal/memberships?userId=X&tenantId=Y` — trả về membership status (dùng bởi identity-service khi refresh token)
- **Bảo mật:** endpoint này chỉ accessible từ internal network, không expose ra gateway

---

#### 3.6 🏗️ Scaffold `gateway-bff` (NestJS :3000)
- Cài packages: `@nestjs/jwt`, `http-proxy-middleware`, `socket.io`, `ioredis`, `@nestjs/throttler`
- Modules: `AuthModule`, `ProxyModule`, `WebSocketModule`, `RateLimitModule`, `HealthModule`

#### 3.7 🧩 JWT Middleware & Tenant Resolution
- Fetch public key (JWKS) từ `identity-service /.well-known/jwks.json` khi startup, cache với TTL + auto-refresh
- Mọi request (trừ `/api/auth/*`): verify JWT, extract `userId`, `tenantId`, `role`
- Inject headers: `X-Tenant-Id`, `X-User-Id`, `X-User-Role`, `X-Correlation-Id`
- ⚠️ Reject 403 nếu JWT `tenantId` không khớp với bất kỳ `tenantId` nào trong request body/path

#### 3.8 🧩 Rate Limiting
- Redis token bucket: `ratelimit:{tenantId}:{route}`
- Limits mặc định: 100 req/min/tenant cho API thông thường, 20 req/min/tenant cho AI endpoints
- Trả `429 Too Many Requests` với `Retry-After` header

#### 3.9 🧩 HTTP Proxy Routing
- Route map từ gateway tới downstream services:
  - `/api/auth/*` → `identity-service:5001`
  - `/api/workspace/*` → `workspace-service:5002`
  - `/api/tickets/*` → `support-service:5003`
  - `/api/kb/*` → `knowledge-service:5004`
  - `/api/search/*` → `search-service:3002`
  - `/api/ai/*` → `ai-service:3003`
  - `/api/campaigns/*` → `campaign-service:5005`
- Strip prefix `/api` trước khi forward
- Forward tất cả headers đã inject

#### 3.10 🧩 WebSocket Endpoint
- Namespace `/ws`, authenticate khi `connect` bằng JWT từ query param hoặc header
- Redis pub/sub adapter (`@socket.io/redis-adapter`) cho multi-instance safe
- Presence tracking: `SET presence:user:{userId} {socketId} EX 30`, heartbeat mỗi 25s
- Room convention: `tenant:{tenantId}`, `ticket:{ticketId}`, `user:{userId}`

✅ **Checkpoint Ngày 3:** Register user → workspace auto-created → login → JWT → gateway proxy hoạt động, WebSocket connect được, RBAC cache hoạt động.

---

### Ngày 4 — Notification Service (Email Foundation)

#### 4.1 🏗️ Scaffold `notification-service` (NestJS :3001)
- Cài packages: `@golevelup/nestjs-rabbitmq`, `handlebars`, `nodemailer`, `ioredis`, `mongoose`
- Modules: `NotificationModule`, `EmailModule`, `InAppModule`, `TemplateModule`
- Kết nối: MongoDB (in-app notifications + templates), Redis (dedup + idempotency)

#### 4.2 ⚙️ MongoDB Collections — notification-service
- `email_templates`: `{ tenant_id, template_key, subject_hbs, body_html_hbs, body_text_hbs, version }`
- `notifications`: `{ id, tenant_id, user_id, type, title, body, read_at, created_at }`
- `inbox_messages`: `{ consumer_name, event_id }` với unique index + TTL 30 ngày

#### 4.3 🧩 Email Engine
- Cấu hình nodemailer với SMTP (Mailpit cho local dev, env var để switch sang SendGrid/Resend cho production)
- `TemplateRenderer`: compile Handlebars templates với variables
- `EmailSender`: gửi email với `dedupe_key` = `{event_id}:{recipient_email}` stored trong Redis TTL 24h (chống gửi trùng khi replay)

#### 4.4 🧩 RabbitMQ Consumers
Mỗi consumer đều: inbox dedup (MongoDB) → process → ack

- **`identity.user.registered.v1`** → gửi verification email
- **`identity.user.email-verified.v1`** → gửi welcome confirmation email
- **`workspace.tenant.created.v1`** → gửi welcome email cho admin tenant mới
- **`workspace.member.invited.v1`** → gửi invite email với magic link
- **`support.ticket.created.v1`** → gửi email confirm cho customer + email notify cho assigned agent
- **`support.ticket.message-added.v1`** → gửi email notify cho recipient (customer hoặc agent)
- **`support.ticket.resolved.v1`** → gửi resolution notification email
- **`support.ticket.sla-breached.v1`** → gửi SLA breach alert email cho manager/admin

#### 4.5 🧩 In-App Notification (WebSocket Push)
- Sau khi save notification vào MongoDB, publish lên Redis pub/sub channel `notifications:{userId}`
- Gateway WebSocket module subscribe và push xuống client
- `GET /api/notifications` — list unread với pagination
- `POST /api/notifications/{id}/read` — mark as read

✅ **Checkpoint Ngày 4:** Register → verification email đến Mailpit, tạo ticket → email notify đến Mailpit, WebSocket push in-app notification hoạt động.

---

### Ngày 5 — Review & Buffer

- Review toàn bộ code tuần 1
- Viết unit tests cho: `RegisterUserHandler`, `LoginUserHandler`, `RefreshTokenHandler`, JWT middleware
- Test OutboxPublisher end-to-end (tạm thời dừng RabbitMQ → OutboxPublisher retry → RabbitMQ restart → events replay)
- Fix bugs và technical debt
- Update CI: thêm test step
- Tạo `docker-compose.dev.yml` — run tất cả services .NET và NestJS cùng một lúc cho local dev

---

## TUẦN 2 — Support Core, Knowledge Service, Outbox/Inbox Hoàn Chỉnh

> **Mục tiêu cuối tuần:** Ticket lifecycle đầy đủ, Knowledge Base CRUD + publish + versioning, Outbox+Inbox hoàn chỉnh, file upload qua Supabase Storage.

---

### Ngày 6–7 — Support Service (Core Nghiệp Vụ)

#### 6.1 🏗️ Scaffold `support-service` (ASP.NET Core 8 :5003)
Cấu trúc Clean Architecture:
```
SupportService/
  Domain/
    Entities/       ← Ticket, TicketMessage, Customer, SlaPolicy, TicketCounter
    Events/         ← TicketCreated, TicketAssigned, MessageAdded, TicketResolved, TicketClosed, SlaBreached
    ValueObjects/   ← TicketStatus, Priority, SentimentType
    Services/       ← SlaCalculator (domain service)
  Application/
    Commands/       ← CreateTicket, AssignTicket, AddMessage, ResolveTicket, CloseTicket
    Queries/        ← GetTicketDetail, GetTicketList, GetTicketHistory
    Consumers/      ← AiClassificationCompleted, AiSummaryCompleted
    DTOs/
  Infrastructure/
    Persistence/    ← SupportDbContext, Migrations
    Repositories/
    Messaging/      ← OutboxPublisher
    BackgroundJobs/ ← SlaBreachScanner (Hangfire)
  API/
    Controllers/    ← TicketsController, CustomersController
    Internal/       ← InternalTicketsController (cho ai-service, search-service)
```
- Cài packages: `MediatR`, `FluentValidation`, `EF Core 8`, `Hangfire`, `Polly`, `StackExchange.Redis`

#### 6.2 ⚙️ Database Schema — `support`
Migrations cho:
- `support.customers`: `id`, `tenant_id`, `email` (unique per tenant), `name`, `phone`, `company`, `tags` (text[]), `created_at`, `updated_at`
- `support.tickets`:
  - `id` (uuid), `tenant_id`, `ticket_no` (varchar), `subject`, `status` (Open/Pending/Resolved/Closed), `priority` (Low/Medium/High/Urgent), `category`, `sentiment`, `tags` (text[])
  - `customer_id`, `assigned_agent_id`, `assigned_team_id`
  - `sla_policy_id`, `first_response_due_at`, `resolution_due_at`, `first_response_at`, `resolved_at`, `closed_at`
  - `ai_summary`, `row_version` (integer default 0)
  - `deleted_at` (soft delete)
  - `created_at`, `updated_at`
- `support.ticket_messages`: `id`, `ticket_id`, `tenant_id`, `sender_type` (Customer/Agent/System/AI), `sender_id`, `body_plain`, `body_html`, `is_internal` (note), `created_at`
- `support.ticket_status_history`: `id`, `ticket_id`, `tenant_id`, `from_status`, `to_status`, `changed_by`, `reason`, `created_at`
- `support.sla_policies`: `id`, `tenant_id`, `name`, `priority`, `first_response_hours`, `resolution_hours`, `business_hours_only`
- `support.ticket_counters`: `tenant_id` (PK), `last_ticket_no` (integer)
- Composite indexes: `tickets(tenant_id, status)`, `tickets(tenant_id, assigned_agent_id)`, `tickets(tenant_id, created_at DESC)`, `ticket_messages(ticket_id, created_at)`

#### 6.3 🧩 Domain — Ticket Aggregate
- `Ticket.Create(tenantId, customerId, subject, body, priority)` → allocates ticket_no, sets initial status Open, raises `TicketCreatedDomainEvent`
- `Ticket.Assign(agentId, assignedBy)` → sets `assigned_agent_id`, calculates SLA deadlines nếu chưa có, raises `TicketAssignedDomainEvent`
- `Ticket.AddMessage(senderId, senderType, body, isInternal)` → raises `MessageAddedDomainEvent`
- `Ticket.Resolve(resolvedBy)` → check status transition valid, set `resolved_at`, raises `TicketResolvedDomainEvent`
- `Ticket.Close(closedBy)` → only from Resolved, raises `TicketClosedDomainEvent`
- `Ticket.ApplyAiClassification(category, priority, sentiment, tags)` → ⚠️ chỉ apply nếu ticket chưa Resolved/Closed và field chưa được manual override
- `Ticket.SoftDelete()` → set `deleted_at`
- `row_version` field: increment tự động khi mọi mutation

#### 6.4 🧩 Ticket Counter — `FOR UPDATE` Pattern
- `AllocateTicketNo(tenantId)`:
  ```sql
  BEGIN;
  SELECT last_ticket_no FROM support.ticket_counters WHERE tenant_id = :tenantId FOR UPDATE;
  UPDATE support.ticket_counters SET last_ticket_no = last_ticket_no + 1 WHERE tenant_id = :tenantId;
  COMMIT;
  -- Format: TF-00001 (prefix per tenant)
  ```
- Upsert nếu counter chưa tồn tại cho tenant

#### 6.5 🧩 Application Commands — CQRS
- **`CreateTicketCommand`**: Idempotency check (`ops.idempotency_keys`), upsert customer, allocate ticket_no, `Ticket.Create()`, save + outbox event, return `TicketDto`
- **`AssignTicketCommand`**: optimistic lock check (`expectedVersion`), `Ticket.Assign()`, save + outbox event
- **`AddMessageCommand`**: Idempotency check, `Ticket.AddMessage()`, save + outbox event
- **`ResolveTicketCommand`**: optimistic lock, `Ticket.Resolve()`, save + outbox event
- **`CloseTicketCommand`**: optimistic lock, `Ticket.Close()`, save + outbox event
- Mọi command mutation: invalidate Redis cache `cache:ticket:{tenantId}:{ticketId}` sau khi commit

#### 6.6 🧩 Application Queries — CQRS Read Side
- **`GetTicketDetailQuery`**: đọc từ PostgreSQL (authoritative), join messages + SLA + history. Cache-aside: check `cache:ticket:{tenantId}:{id}` TTL 300s trước, nếu miss → query PG + stampede prevention (mutex + double-check)
- **`GetTicketListQuery`**: đọc từ Elasticsearch (search service sẽ maintain index) — forward request sang search-service qua internal HTTP
- **`GetTicketHistoryQuery`**: đọc `ticket_status_history` từ PostgreSQL

#### 6.7 🧩 Idempotency Pattern
- `ops.idempotency_keys`: `id` (uuid), `request_hash` (sha256 của payload), `response_body` (jsonb), `locked_until`, `created_at`
- Behavior:
  - Same `Idempotency-Key` + same hash → return stored response (200 OK, không execute lại)
  - Same `Idempotency-Key` + different hash → return 409 Conflict
  - Cleanup: Hangfire job xóa records > 24h

#### 6.8 🧩 Optimistic Concurrency
- Client gửi `expectedVersion` trong request body
- Handler:
  ```sql
  UPDATE support.tickets SET ..., row_version = row_version + 1
  WHERE id = :id AND tenant_id = :tenantId AND row_version = :expectedVersion AND deleted_at IS NULL
  ```
- 0 rows updated → return `409 Conflict` với body `{ "error": "version_conflict", "currentVersion": N }`

#### 6.9 🧩 SLA Calculation
- `SlaCalculator.CalculateDeadlines(policy, createdAt)`: tính `first_response_due_at` và `resolution_due_at` từ policy hours (có support business hours nếu `business_hours_only = true`)
- `SlaBreachScanner` (Hangfire): cron mỗi 5 phút, query tickets `WHERE resolution_due_at < NOW() AND status NOT IN ('Resolved', 'Closed') AND tenant_id = :tenantId` → thêm outbox event `support.ticket.sla-breached.v1`

#### 6.10 🧩 Event Consumers trong Support Service
- **`ai.classification.completed.v1`**: inbox dedup → `Ticket.ApplyAiClassification()` → save → broadcast WebSocket `ticket.enriched` → invalidate cache
- **`ai.summary.completed.v1`**: inbox dedup → update `ai_summary` field → save → invalidate cache

#### 6.11 🧩 Internal HTTP Endpoints
- `GET /internal/tickets/{id}?tenantId={tenantId}` — dùng bởi ai-service khi classify/summarize
- `GET /internal/customers/{email}?tenantId={tenantId}` — dùng bởi ai-service khi build context

#### 6.12 🧩 Audit Log
- Ghi `ops.audit_logs` (inline cùng transaction) cho mỗi status change: `{ tenant_id, entity_type, entity_id, action, actor_id, old_value, new_value, created_at }`

✅ **Checkpoint Ngày 7:** Tạo ticket → ticket_no allocated → outbox event → notification email → AI classification queue có message. Assign, reply, resolve, close hoạt động. 409 Conflict khi version mismatch.

---

### Ngày 8 — Knowledge Service

#### 8.1 🏗️ Scaffold `knowledge-service` (ASP.NET Core 8 :5004)
Tương tự cấu trúc Clean Architecture với:
- Domain: `Article`, `ArticleVersion`, `ArticleTag`
- Events: `ArticleDrafted`, `ArticlePublished`, `ArticleUnpublished`
- Application: `CreateArticle`, `UpdateArticle`, `PublishArticle`, `UnpublishArticle`, `GetArticle`, `ListArticles`

#### 8.2 ⚙️ Database Schema — `knowledge`
- `knowledge.articles`: `id`, `tenant_id`, `slug` (unique per tenant), `current_version_id` (FK), `status` (Draft/Published/Archived), `category`, `author_id`, `published_by`, `published_at`, `deleted_at`, `created_at`, `updated_at`
- `knowledge.article_versions`: `id`, `article_id`, `tenant_id`, `version_number` (auto increment per article), `title`, `body_html`, `body_plain` (plain text extract), `meta_description`, `tags` (text[]), `word_count`, `author_id`, `created_at`
- `knowledge.article_tags`: `id`, `tenant_id`, `name`, `slug`
- Indexes: `articles(tenant_id, status)`, `articles(tenant_id, slug)`, `article_versions(article_id, version_number DESC)`

#### 8.3 🧩 Versioning Logic
- Mỗi lần `UpdateArticle` → tạo `ArticleVersion` mới (không update in-place), increment `version_number`
- `PublishArticle` → cập nhật `articles.current_version_id` → trỏ sang version mới nhất, set status Published, thêm outbox event `knowledge.article.published.v1`
- `UnpublishArticle` → set status Archived, thêm outbox event `knowledge.article.unpublished.v1`
- Soft delete: `deleted_at` không xóa versions

#### 8.4 Supabase Storage File Upload
- Signed URL flow:
  - `POST /api/kb/articles/{id}/attachments/presign` → tạo signed upload URL từ Supabase Storage, trả URL cho FE
  - FE upload trực tiếp lên Supabase Storage (không qua backend)
  - `POST /api/kb/articles/{id}/attachments/confirm` → ghi `knowledge.article_attachments` record

#### 8.5 🧩 Outbox Events — knowledge-service
- `knowledge.article.published.v1`: `{ articleId, tenantId, slug, title, body_plain, tags, tenantId, version }`
  → search-service consumer sẽ index vào Elasticsearch (bao gồm embed text)
- `knowledge.article.unpublished.v1`: `{ articleId, tenantId }`
  → search-service consumer sẽ xóa khỏi Elasticsearch index

✅ **Checkpoint Ngày 8:** Tạo article → draft → update (new version) → publish → outbox event fired → search-service (tuần 3) sẽ pick up và index.

---

### Ngày 9–10 — Outbox/Inbox Pattern Hoàn Chỉnh + Buffer

#### 9.1 🧩 Hoàn thiện OutboxPublisher cho tất cả .NET services
- Verify OutboxPublisher hoạt động đúng cho: identity-service, workspace-service, support-service, knowledge-service
- Test scenario: Kill RabbitMQ → tạo vài tickets → restart RabbitMQ → verify tất cả events replay
- Kiểm tra `claimed_at` index hoạt động đúng
- Verify stale lock reclaim job hoạt động

#### 9.2 🧩 Hoàn thiện Inbox Pattern cho tất cả consumers
- Verify inbox dedup hoạt động trong: notification-service, workspace-service, identity-service
- Test replay: publish cùng 1 event 3 lần → consumer chỉ xử lý 1 lần
- Test stuck-processing cleanup: giả lập consumer crash giữa chừng → verify cleanup job reset status

#### 9.3 🧩 Dead Letter Queue Handling
- Trong notification-service: nếu consumer fail > max retry → nack → message vào DLQ
- Tạo endpoint internal: `POST /internal/dlq/replay` — cho phép replay message từ DLQ về queue chính
- Grafana alert rule (seed vào provisioning): DLQ count > 0

#### 9.4 🧩 `ops.inbox_messages` Retention Policy
- Hangfire job (trong support-service): xóa `ops.inbox_messages WHERE created_at < NOW() - 30 days`
- MongoDB `inbox_messages` (ai-service, notification-service): TTL index `created_at` với expiry 30 ngày

#### 9.5 Unit Tests — Core Patterns
- Test `OutboxPublisher`: happy path, broker down → retry, max retry → mark failed
- Test `InboxRepository`: duplicate insert → return false (skip), first insert → return true (process)
- Test `CreateTicketHandler`: idempotency key replay, version conflict

---

## TUẦN 3 — Search, AI, Real-time Chat, Caching & Performance

> **Mục tiêu cuối tuần:** Hybrid search hoạt động, RAG chatbot với citation + escalation, WebSocket real-time, cache stampede prevention, rate limiting hoàn chỉnh.

---

### Ngày 11–12 — Search Service

#### 11.1 🏗️ Scaffold `search-service` (NestJS :3002)
- Packages: `@elastic/elasticsearch`, `@golevelup/nestjs-rabbitmq`, `mongoose` (inbox dedup)
- Modules: `IndexingModule`, `SearchModule`, `HealthModule`

#### 11.2 🧩 RabbitMQ Consumers — Indexing
Mỗi consumer: inbox dedup (MongoDB) → xử lý → ack

- **`knowledge.article.published.v1`** → upsert Elasticsearch `kb_articles_v1`:
  - Index fields: `tenant_id`, `article_id`, `title`, `body_plain`, `tags`, `slug`, `published_at`, `aggregate_version`
  - ⚠️ `aggregate_version` guard: check `IF incoming.version > current_doc.aggregate_version THEN upsert ELSE skip` (Painless script trong ES để atomic check+update)
  - Embedding sẽ được add bởi ai-service (tuần sau), lúc này chỉ index text fields

- **`knowledge.article.unpublished.v1`** → delete document từ `kb_articles_v1`

- **`support.ticket.created.v1`** → upsert `tickets_v1` với basic fields
- **`support.ticket.assigned.v1`** → update `assigned_agent_id` trong `tickets_v1`
- **`support.ticket.resolved.v1`** → update `status = 'Resolved'` trong `tickets_v1`
- **`support.ticket.closed.v1`** → update `status = 'Closed'` trong `tickets_v1`
- **`support.ticket.message-added.v1`** → update `last_message_at` trong `tickets_v1`

#### 11.3 🧩 Hybrid Search Endpoint
- `GET /search?q=...&type=tickets|articles|all&tenantId=...&page=1&limit=20`
- Elasticsearch query:
  ```json
  {
    "knn": { "field": "embedding", "query_vector": [...], "k": 5, "filter": { "term": { "tenant_id": "uuid" } } },
    "query": { "bool": { "must": { "multi_match": { "query": "...", "fields": ["title^2", "body_plain"] } }, "filter": { "term": { "tenant_id": "uuid" } } } },
    "rank": { "rrf": {} }
  }
  ```
- Khi embedding chưa có (article mới index, chưa embed) → fallback pure BM25
- Return: `{ results: [...], total, took_ms }`

#### 11.4 🧩 Projection Freshness Metric
- Custom metric: `search_projection_lag_seconds` = `(indexed_at - occurred_at)` trong milliseconds
- Emit metric khi index document
- Target SLO: p95 < 5s

✅ **Checkpoint Ngày 12:** Publish KB article → search-service consumer indexes → `GET /api/search?q=...` trả kết quả. Tạo ticket → ticket xuất hiện trong search.

---

### Ngày 13–14 — AI Service

#### 13.1 🏗️ Scaffold `ai-service` (NestJS :3003)
- Packages: `langchain`, `@langchain/openai`, `@elastic/elasticsearch`, `mongoose`, `@golevelup/nestjs-rabbitmq`, `bullmq`
- Modules: `LlmModule`, `EmbeddingModule`, `RagModule`, `ClassifyModule`, `SuggestModule`, `SummaryModule`, `MemoryModule`
- MongoDB collections: `ai_runs`, `chat_sessions`, `customer_memory`, `inbox_messages`

#### 13.2 ⚙️ MongoDB Collections — ai-service
- `ai_runs`: `{ id, tenant_id, type (classify/rag/suggest/summarize), ticket_id?, session_id?, model, prompt_tokens, completion_tokens, latency_ms, confidence?, result (jsonb), escalated?, correlation_id, created_at }` — TTL index 90 ngày
- `chat_sessions`: `{ session_id, tenant_id, customer_email, messages: [...], created_at, updated_at }` — TTL 7 ngày
- `customer_memory`: `{ tenant_id, customer_email, summary, preferences, history_highlights, updated_at }` — permanent
- `inbox_messages`: `{ consumer_name, event_id }` — unique index, TTL 30 ngày

#### 13.3 🔗 LLM Factory
- `LlmProvider` interface với 2 implementations:
  - `OpenAiProvider`: GPT-4o-mini, nhiệt độ configurable, timeout 10s, Polly circuit breaker (5 fail/30s → OPEN → fallback Ollama)
  - `OllamaProvider`: llama3.2:3b local, fallback khi OpenAI down
- `EmbeddingProvider`: text-embedding-3-small (1536 dims), fallback `nomic-embed-text` via Ollama

#### 13.4 🧩 Auto-Classify Consumer
- **`support.ticket.created.v1`** consumer:
  1. Inbox dedup (MongoDB)
  2. Per-tenant rate limit: Redis `INCR ai:ratelimit:{tenantId}` sliding 60s, > 20 → nack + sleep backoff
  3. Fetch ticket từ support-service `GET /internal/tickets/{id}`
  4. Build classify prompt (system + user), call LLM (temperature 0.1)
  5. Log vào MongoDB `ai_runs`
  6. Publish `ai.classification.completed.v1` (qua direct RabbitMQ publish, không qua outbox vì NestJS không có PG)

#### 13.5 🧩 Embedding Pipeline
- **`knowledge.article.published.v1`** consumer:
  1. Inbox dedup
  2. Chunk article body (512 tokens, 50 token overlap)
  3. `openai.embeddings.create()` cho từng chunk
  4. Upsert Elasticsearch `kb_articles_v1` với `embedding` field (dense_vector)
  5. Nếu nhiều chunks → upsert từng chunk doc với `{ article_id, chunk_index, tenant_id, embedding, body_plain_chunk }`
- BullMQ queue `embedding-jobs` cho xử lý batch (không block consumer)

#### 13.6 🧩 RAG Pipeline — `POST /api/ai/ask`
Đây là endpoint public widget chatbot:
1. Validate request: `{ query, sessionId, tenantId, customerEmail? }`
2. **Embed query**: `openai.embeddings.create(query)` → vector
3. **Hybrid retrieve** từ Elasticsearch (bắt buộc filter `tenant_id`): kNN (k=5) + BM25, trả top 3 chunks
4. **Augment context**: fetch `customer_memory` từ MongoDB (nếu có `customerEmail`), fetch short-term context từ Redis `ai_context:{sessionId}` TTL 30min
5. **Generate**: system prompt "Answer ONLY based on provided KB articles. Always cite sources.", LLM call (temperature 0.3)
6. **Evaluate confidence** (Score-based, không cần extra LLM call):
   - `scoreNormalized = min(maxEsScore / 0.7, 1.0)`
   - `keywordOverlap = matchedTerms / queryTerms`
   - `confidence = (scoreNormalized * 0.7) + (keywordOverlap * 0.3)`
   - `confidence >= 0.75` → serve answer + citations
   - `confidence < 0.75` → return `{ escalate: true, reason: "low_confidence" }` (support-service sẽ tạo ticket)
7. **Store**: save chat session MongoDB + update Redis context + async schedule `customer_memory` update

#### 13.7 🧩 Agent Assist — `POST /api/ai/tickets/{id}/suggest-reply`
1. Fetch ticket + messages từ support-service `/internal/tickets/{id}`
2. Fetch customer memory từ MongoDB
3. Hybrid retrieve từ KB (ES) với ticket context
4. Build prompt: "You are a support agent. Draft a helpful reply based on the KB and conversation history."
5. LLM call (temperature 0.5)
6. Cache result: Redis `ai_suggest:{tenantId}:{ticketId}` TTL 300s
7. Log `ai_runs`
8. Return `{ suggestedReply, confidence, sources }`

#### 13.8 🧩 Ticket Summary Consumer
- **`support.ticket.resolved.v1`** consumer:
  1. Inbox dedup
  2. Fetch ticket + all messages từ support-service
  3. Build summarize prompt
  4. LLM call
  5. Publish `ai.summary.completed.v1` → support-service sẽ update `ai_summary` field
  6. Update `customer_memory` MongoDB: upsert với history highlight

#### 13.9 🧩 Backpressure
- `channel.prefetch(5)` cho tất cả RabbitMQ consumers
- Per-tenant rate limiter: Redis sliding window (xem 13.4)
- Polly circuit breaker trên OpenAI HTTP client
- Emit metrics: `ai_circuit_breaker_state`, `ai_rate_limit_triggers_total`

✅ **Checkpoint Ngày 14:** Tạo ticket → ai-service classify → category/priority/sentiment update. Chat widget → RAG trả lời với citation. Confidence thấp → escalate signal. Resolve ticket → ai_summary update.

---

### Ngày 15 — Caching, Rate Limiting & Performance

#### 15.1 🧩 Cache-Aside Pattern (Redis) — tất cả .NET services
- Ticket detail: `cache:ticket:{tenantId}:{ticketId}` TTL 300s
- KB article public: `cache:kb:{tenantId}:{slug}` TTL 3600s
- User permissions: `user_permissions:{userId}:{tenantId}` TTL 3600s
- Dashboard stats: `cache:stats:{tenantId}:daily` TTL 60s

#### 15.2 🧩 Cache Stampede Prevention
Implement trong `CacheService`:
```
1. GET cache → HIT: return
2. MISS → SET lockKey NX PX 10000 (Lua compare-and-set)
3. Lock acquired: Double-check GET → still miss → query DB → SET cache TTL 300s → release lock (Lua compare-and-delete)
4. Lock not acquired: sleep(50ms) → GET cache
```

#### 15.3 🧩 Cache Invalidation
- Sau mỗi ticket mutation: evict `cache:ticket:{tenantId}:{ticketId}`
- Sau mỗi article publish/unpublish: evict `cache:kb:{tenantId}:{slug}`
- Sau mỗi role/membership change trong workspace-service: evict `user_permissions:{userId}:{tenantId}`

#### 15.4 🧩 Rate Limiting nâng cao — Gateway
- Differentiate limits: `/api/ai/*` → 20 req/min/tenant; `/api/search/*` → 60 req/min/tenant; default → 100 req/min/tenant
- Custom middleware ghi metric `rate_limit_triggers_total{tenantId, route}`

#### 15.5 🔗 k6 Load Test (đợt 1)
- Script `tests/k6/smoke.js`: 10 VU × 2 phút, endpoint: GET /api/tickets/{id}
- Mục tiêu: identify slow queries, not hitting targets
- `EXPLAIN ANALYZE` top 3 slow queries → thêm indexes nếu cần
- Record baseline: p50, p95, p99 latency

---

## TUẦN 4 — Campaign, Deploy Production, Observability, Polish

> **Mục tiêu cuối tuần:** Deploy thật lên VPS với URL live, CI/CD tự động, Grafana dashboard, uptime monitor, demo data seeded.

---

### Ngày 16 — Campaign Service (Optional nhưng nên làm)

> **Nếu đang bị trễ thời gian → skip campaign-service, jump sang Ngày 17.**

#### 16.1 🏗️ Scaffold `campaign-service` (ASP.NET Core 8 :5005)
- Domain: `Campaign`, `CampaignDispatch`, `Segment`
- Application: `CreateCampaign`, `ScheduleFollowUp`, `DispatchCampaign`
- Infrastructure: Hangfire (persistent jobs), OutboxPublisher

#### 16.2 ⚙️ Database Schema — `campaign`
- `campaign.campaigns`: `id`, `tenant_id`, `name`, `trigger` (ticket_resolved), `delay_minutes`, `template_id`, `segment_rules` (jsonb), `status` (Active/Paused), `created_at`
- `campaign.segments`: `id`, `tenant_id`, `name`, `rules` (jsonb: tag filters, category filters)
- `campaign.campaign_dispatches`: `id`, `campaign_id`, `ticket_id` (UNIQUE với campaign_id), `recipient_id`, `status` (Scheduled/Sent/Failed), `sent_at`, `created_at`

#### 16.3 🧩 Consumer + Guards
- **`support.ticket.resolved.v1`** consumer:
  1. Inbox dedup
  2. **Guard A** (Redis advisory lock 10 phút): `SET campaign:lock:{campaignId}:{ticketId} NX PX 600000`
  3. Evaluate segment rules
  4. Match → tạo Hangfire job delay `campaign.delay_minutes`
  5. Release lock sau khi job scheduled
- **Hangfire job** khi execute:
  1. Check ticket vẫn Resolved (không bị reopen)
  2. **Guard B** (DB UNIQUE): `INSERT campaign_dispatches ON CONFLICT (campaign_id, ticket_id) DO NOTHING`
  3. 0 rows → skip
  4. Throttle: Redis counter `dispatch:{tenantId}:{minute}` > 50 → sleep 1s + retry
  5. Publish `campaign.dispatch.requested.v1` → notification-service
- **notification-service consumer** `campaign.dispatch.requested.v1` → render Handlebars template → send email

---

### Ngày 17 — Demo Data Seeding

#### 17.1 🧩 Seeder Script
Tạo `scripts/seed-demo-data.ts` (hoặc C# CLI tool) để seed:

**Tenant TaskFlow:**
- 1 admin, 3 agents, 1 manager, 1 analyst
- 25 KB articles (categories: Getting Started, Billing, Integrations, Troubleshooting, API)
- 100 tickets với realistic subjects/messages (mix: Open, Pending, Resolved, Closed)
- SLA policy: High → 2h response / 24h resolution
- Feature flags: `ai_assist: true`, `campaigns: true`

**Tenant InvoiceFox:**
- 1 admin, 2 agents
- 20 KB articles (categories: Invoicing, Payments, Reports, Account)
- 80 tickets
- SLA policy: standard

**Mỗi KB article** phải có body đủ dài để test RAG (> 200 words)  
**Mỗi ticket** phải có ít nhất 2–3 messages để test summary

#### 17.2 🧩 Trigger Embedding Pipeline
- Sau khi seed articles → publish `knowledge.article.published.v1` events thủ công (hoặc qua seeder)
- Wait cho ai-service embed tất cả articles → verify trong Elasticsearch `kb_articles_v1` có `embedding` field

---

### Ngày 18 — Docker Compose Production & VPS Setup

#### 18.1 ⚙️ `docker-compose.prod.yml`
Tạo production Docker Compose tách biệt với dev:
- Resource limits: memory + CPU limits cho mỗi container
- Health checks với `start_period` phù hợp
- Named volumes cho tất cả persistent data
- Restart policy: `unless-stopped`
- Không expose ports internal services ra ngoài (chỉ gateway + nginx)
- Environment variables từ `.env.prod` (không commit vào git)

#### 18.2 🎯 Dockerfile cho Mỗi Service
- **ASP.NET Core**: multi-stage build (`sdk` → `runtime:8.0-alpine`), non-root user
- **NestJS**: multi-stage build (`node:20-alpine` build → `node:20-alpine` runtime), non-root user
- Minimize image size: copy chỉ `dist/` và production `node_modules`
- `.dockerignore` đầy đủ

#### 18.3 🎯 VPS Setup
- Chọn VPS: DigitalOcean Droplet ($12/mo, 2GB RAM) hoặc Hetzner (rẻ hơn)
- OS: Ubuntu 24.04 LTS
- Cài đặt: Docker Engine, Docker Compose Plugin, Git, Nginx, Certbot (Let's Encrypt)
- SSH key authentication, disable password login
- UFW firewall: chỉ mở port 22 (SSH), 80 (HTTP), 443 (HTTPS)
- Tạo deploy user (không dùng root)

#### 18.4 ⚙️ Nginx Config
Tạo `nginx/conf.d/signaldesk.conf`:
- `upstream gateway` → `gateway-bff:3000`
- SSL/TLS với Let's Encrypt (Certbot), auto-renew
- HTTP → HTTPS redirect
- WebSocket proxy: `proxy_http_version 1.1`, `Upgrade`, `Connection` headers
- Gzip compression
- Rate limiting: `limit_req_zone` + `limit_req`
- Security headers: `X-Frame-Options`, `X-XSS-Protection`, `Content-Security-Policy`

#### 18.5 ⚙️ Secrets Management
- Tạo `.env.prod` với tất cả secrets: DB passwords, JWT private key, OpenAI API key, SMTP credentials
- Store secrets trong GitHub Secrets (cho CI/CD) hoặc sử dụng `docker secret` nếu dùng Swarm
- Generate RS256 key pair: `openssl genrsa -out private.pem 2048` + `openssl rsa -in private.pem -pubout -out public.pem`

---

### Ngày 19 — CI/CD Pipeline Production

#### 19.1 🎯 GitHub Actions — `deploy-production.yml`
```yaml
# Trigger: push to main
# Jobs:
1. ci:
   - build-and-test (dotnet test + npm run test + lint)
   - docker-build-push (build images, push to GHCR)
   
2. deploy (depends on: ci):
   - SSH vào VPS
   - docker compose -f docker-compose.prod.yml pull
   - docker compose -f docker-compose.prod.yml up -d --remove-orphans
   - Chạy database migrations
   - Health check all services
   - Rollback nếu health check fail
```

#### 19.2 ⚙️ Database Migrations trong CI/CD
- Strategy: chạy migrations trước khi start services
- `dotnet ef database update` cho mỗi .NET service (theo thứ tự: identity → workspace → support → knowledge → campaign)
- Nếu migration fail → abort deploy, không start services

#### 19.3 ⚙️ Health Check Gate sau Deploy
- Script `scripts/health-check.sh`: curl `/health/ready` cho mỗi service
- Max retries: 30 lần × 5s interval = 2.5 phút timeout
- Nếu bất kỳ service nào không ready → rollback bằng `docker compose up -d --scale {service}=0` + restart previous

✅ **Checkpoint Ngày 19:** Push code lên main → CI build → test → build Docker images → push GHCR → deploy VPS tự động → health check pass. URL live accessible.

---

### Ngày 20 — Observability Stack

#### 20.1 🔗 OpenTelemetry — .NET Services
- Cài `OpenTelemetry.AspNetCore`, `OpenTelemetry.Exporter.Jaeger`
- Auto-instrument: HTTP (incoming + outgoing), EF Core, RabbitMQ
- Custom spans: OutboxPublisher publish span, Consumer processing span
- Attributes: `tenant.id`, `correlation.id`, `ticket.id` khi có
- Export: Jaeger/Tempo endpoint

#### 20.2 🔗 OpenTelemetry — NestJS Services
- Cài `@opentelemetry/sdk-node`, `@opentelemetry/auto-instrumentations-node`
- Auto-instrument: HTTP (incoming + outgoing), MongoDB, Elasticsearch, Redis
- Custom spans: RAG pipeline steps (embed → retrieve → generate → evaluate)
- Attributes: `tenant.id`, `ai.model`, `ai.confidence`
- Export: Jaeger/Tempo endpoint

#### 20.3 🔗 Prometheus Metrics
- **ASP.NET**: `prometheus-net.AspNetCore` — auto HTTP metrics + custom metrics via `Counter`, `Histogram`
- **NestJS**: `prom-client` — custom metrics
- Custom metrics cần implement (xem section 10.9 trong SIGNALDESK_AI_BE_v6_0.md):
  - `outbox_publish_duration_seconds` histogram
  - `inbox_duplicate_total` counter
  - `cache_hit_total` / `cache_miss_total` counter
  - `ai_inference_duration_seconds{type}` histogram
  - `ai_confidence_score` histogram
  - `queue_message_age_seconds{queue}` gauge
  - `circuit_breaker_state{service}` gauge
  - `version_conflict_total` counter

#### 20.4 🔗 Grafana — Dashboard Provisioning
Tạo Grafana dashboard JSON files trong `monitoring/grafana/dashboards/`:
- `dashboard-api-health.json` — API Health (latency p50/p95/p99, error rate, SLO burn rate, 409 conflicts)
- `dashboard-queue-health.json` — Queue Health (queue depth, DLQ count, oldest message age, outbox pending)
- `dashboard-ai-pipeline.json` — AI Pipeline (AI latency, confidence, escalation rate, circuit breaker state)
- `dashboard-tenant-health.json` — Tenant Health (per-tenant metrics)
- `dashboard-infrastructure.json` — Infrastructure (PgBouncer, PostgreSQL, Redis, Elasticsearch)

Thêm Grafana alerting rules:
- p95 > 200ms → warning
- DLQ count > 0 → critical
- Circuit breaker OPEN > 30s → critical
- Cache hit rate < 60% → warning

#### 20.5 🔗 Loki + Promtail
- Cấu hình Promtail để scrape Docker container logs
- Label: `service`, `tenant_id`, `level`
- Query example trong Grafana: `{service="support-service"} |= "ticket.created"`

#### 20.6 🔗 Sentry Error Tracking
- ASP.NET: `Sentry.AspNetCore` — unhandled exceptions, performance monitoring
- NestJS: `@sentry/node` — unhandled exceptions
- Ignore: `ValidationException`, `NotFoundException` (expected errors)
- Alert: unknown 5xx errors

#### 20.7 🔗 UptimeRobot Setup
- Create free account tại uptimerobot.com
- Add monitors: `GET https://yourdomain.com/api/health` (từ gateway health endpoint)
- Alert: email khi downtime > 1 phút
- Badge: embed vào README

---

### Ngày 21 — k6 Load Testing & Final Polish

#### 21.1 🔗 k6 Load Test (Final)
Tạo test scripts `tests/k6/`:
- `scenarios/ticket-create.js` — POST /api/tickets (Idempotency-Key header)
- `scenarios/ticket-list.js` — GET /api/tickets?q=...
- `scenarios/rag-chat.js` — POST /api/ai/ask
- `load-test.js` — kết hợp: 100 VU × 5 phút, ramp up 1 phút, ramp down 1 phút

Targets (document trong README):
- p95 API latency < 200ms
- p99 API latency < 500ms
- 0% error rate (không tính 409 expected conflicts)
- > 200 req/min throughput

`EXPLAIN ANALYZE` bất kỳ query nào miss targets → thêm index → re-run.

#### 21.2 🧩 Integration Tests
Tạo `tests/integration/` với Testcontainers:
- `ticket-lifecycle.test.ts`: tạo ticket → assign → reply → resolve → verify outbox → verify ES projection
- `rag-pipeline.test.ts`: publish article → embed → chat query → verify citation
- `inbox-idempotency.test.ts`: replay event 3 lần → verify chỉ xử lý 1 lần

#### 21.3 🧩 Health Check Tổng Hợp — Gateway
- `GET /api/health` — aggregate readiness của tất cả downstream services
- Gọi parallel `GET {service}/health/ready` cho tất cả services, timeout 3s mỗi cái
- Return:
  ```json
  {
    "status": "healthy|degraded|unhealthy",
    "services": {
      "identity": "healthy",
      "workspace": "healthy",
      "support": "healthy",
      "knowledge": "healthy",
      "notification": "healthy",
      "search": "healthy",
      "ai": "degraded"
    }
  }
  ```

#### 21.4 📝 Documentation
- OpenAPI/Swagger:
  - Mỗi .NET service: tự động sinh từ XML comments + annotations → `/swagger`
  - Mỗi NestJS service: `@nestjs/swagger` decorators → `/api-docs`
  - Gateway aggregate: gom tất cả specs tại `/api/docs`
- `README.md` tổng quan: architecture diagram, setup guide, environment variables, demo accounts
- Event contract docs: JSON Schema files trong `libs/contracts/events/`

#### 21.5 🎯 Final Production Deploy & Verify
1. Push final code → CI/CD tự động deploy
2. Smoke test trên production URL:
   - Register → login → tạo ticket → kiểm tra email trong production mailbox
   - Chat widget → RAG query
   - Jaeger trace của 1 full request
3. Verify Grafana dashboards đang nhận data
4. Verify UptimeRobot đang green

---

## TUẦN 5 (Buffer / Stretch Goals)

> Chỉ làm nếu còn thời gian và muốn maximize CV impact

### Stretch 1 — Contract Testing (Pact)
- HTTP contract: gateway → support-service
- RabbitMQ event schema: JSON Schema validation cho tất cả events trong `libs/contracts/events/`
- CI integration: `pact:verify` bước trong ci.yml

### Stretch 2 — Kafka Migration (Analytics Stream)
- Thêm Kafka container vào `docker-compose.infra.yml`
- Migrate analytics events (ticket metrics, SLA metrics) từ RabbitMQ fan-out sang Kafka topic
- Consumer group: analytics-service (embedded trong workspace-service hoặc tách service mới)
- Document lý do migrate: ordered events, consumer group lag tracking

### Stretch 3 — gRPC Internal Communication
- identity-service ↔ workspace-service: thay internal HTTP bằng gRPC cho `CheckMembership` RPC
- Define `.proto` trong `libs/contracts/grpc/`

### Stretch 4 — Feature Flag nâng cao
- Rollout by percentage: `rollout_percentage` field trong `workspace.feature_flags`
- A/B test: enable feature cho 50% users trong tenant

---

## Checklist Cuối Dự Án

### Core (bắt buộc)
- [ ] Multi-tenant auth + RBAC hoạt động (3 roles trở lên)
- [ ] Ticket lifecycle đầy đủ (Create → Assign → Reply → Resolve → Close)
- [ ] Knowledge Base CRUD + versioning + publish workflow
- [ ] RabbitMQ + Outbox Pattern (at-least-once delivery, proven by kill RabbitMQ test)
- [ ] Inbox Pattern (idempotency proven by replay test)
- [ ] Hybrid search (keyword + semantic) hoạt động
- [ ] RAG chatbot với citation + confidence-based escalation
- [ ] Deploy lên VPS với URL live (HTTPS)
- [ ] CI/CD tự động (push to main → deploy)
- [ ] Grafana dashboard ít nhất 1 dashboard có data thật

### Advanced (nên có)
- [ ] WebSocket real-time (ticket update, in-app notification)
- [ ] AI auto-classify (category + priority sau khi tạo ticket)
- [ ] Agent assist (suggest reply)
- [ ] Ticket summary sau resolve
- [ ] Customer memory (RAG context cải thiện)
- [ ] Cache stampede prevention
- [ ] Circuit breaker (Polly) trên OpenAI client
- [ ] k6 load test documented (p95 < 200ms)
- [ ] OpenTelemetry traces trong Jaeger

### Optional
- [ ] Campaign service (follow-up automation)
- [ ] Integration tests với Testcontainers
- [ ] Contract tests (Pact)
- [ ] Kafka migration

---

## Dependency Map — Service Build Order

```
Ngày 1: Infra (không phụ thuộc gì)
         ↓
Ngày 2: identity-service (phụ thuộc: PostgreSQL, RabbitMQ, Hangfire)
         ↓
Ngày 3: workspace-service + gateway-bff (phụ thuộc: identity-service, PostgreSQL, Redis, RabbitMQ)
         ↓
Ngày 4: notification-service (phụ thuộc: RabbitMQ, MongoDB, Redis)
         ↓
Ngày 6–7: support-service (phụ thuộc: PostgreSQL, RabbitMQ, Redis, Hangfire)
         ↓
Ngày 8: knowledge-service (phụ thuộc: PostgreSQL, RabbitMQ, Supabase Storage external)
         ↓
Ngày 11–12: search-service (phụ thuộc: Elasticsearch, RabbitMQ, MongoDB)
         ↓
Ngày 13–14: ai-service (phụ thuộc: Elasticsearch, MongoDB, Redis, RabbitMQ, OpenAI/Ollama, support-service internal HTTP)
         ↓
Ngày 16: campaign-service (phụ thuộc: PostgreSQL, RabbitMQ, Hangfire, notification-service)
         ↓
Ngày 17+: deploy, observability, testing
```

---

## Quick Reference — Ports

| Service | Port | Notes |
|---------|------|-------|
| gateway-bff | 3000 | Entry point duy nhất |
| identity-service | 5001 | .NET |
| workspace-service | 5002 | .NET |
| support-service | 5003 | .NET |
| knowledge-service | 5004 | .NET |
| notification-service | 3001 | NestJS |
| search-service | 3002 | NestJS |
| ai-service | 3003 | NestJS |
| campaign-service | 5005 | .NET (optional) |
| PostgreSQL | 5432 | direct (chỉ dùng nội bộ) |
| PgBouncer | 6432 | services kết nối qua đây |
| MongoDB | 27017 | |
| Redis | 6379 | |
| RabbitMQ | 5672 / 15672 | 15672 = Management UI |
| Elasticsearch | 9200 | |
| Supabase Storage | external managed service | signed URL upload/download |
| Prometheus | 9090 | |
| Grafana | 3100 | |
| Jaeger | 16686 | UI |
| Loki | 3300 | |
| Mailpit | 1025 / 8025 | 8025 = Web UI |
| Ollama | 11434 | |

---

## Ghi Chú Quan Trọng

1. **Không commit secrets** — `.env.prod`, private keys, API keys vào git. Dùng `.gitignore` + GitHub Secrets.

2. **Test Outbox/Inbox bằng chaos** — Kill RabbitMQ 5 phút khi có data đang chạy → restart → verify events được replay, không bị mất, không bị duplicate.

3. **Verify tenant isolation** — Tạo JWT cho tenant A, thử call API với tenantId của tenant B trong path/body → phải nhận 403.

4. **RAG không hallucinate** — Hỏi câu không có trong KB → phải nhận escalate signal, không phải câu trả lời bịa.

5. **Demo data trước khi deploy** — Seed ít nhất 50+ tickets và 20+ KB articles với embedding để RAG có ngữ cảnh demo tốt.

6. **Grafana có data thật** — Ít nhất 1 Grafana dashboard hiển thị metrics thật từ production traffic (không chỉ là screenshot local).

7. **URL live trong README** — Demo URL + demo accounts (TaskFlow + InvoiceFox) phải accessible công khai.

---

*Timeline v1.0 — SignalDesk AI BE — Dành riêng cho Backend. FE chạy song song ở agent khác.*

# SignalDesk AI — Backend
## Source of Truth: Architecture, Services, Patterns, Concurrency, System Design

> **File này là nguồn sự thật duy nhất cho phần Backend của SignalDesk AI.**
> Phiên bản: `v6.0-BE` — fixes: NestJS inbox dedup strategy (Redis/MongoDB thay vì PG cho services không có PG),
> OutboxPublisher service_name filter + index, inbox stuck-processing cleanup job,
> ES stale event guard atomic (Painless script), search-service consume ticket.assigned.v1,
> notification-service null agent_id guard, inbox_messages retention policy,
> calculateConfidence defined, refresh token security after membership revoke,
> soft delete cho tickets + articles, claimed_at index, campaign lock comment clarified.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Kiến Trúc Hệ Thống](#2-kiến-trúc-hệ-thống)
3. [Tech Stack BE & Lý Do Chọn](#3-tech-stack-be--lý-do-chọn)
4. [Danh Sách Services — Chi Tiết Đầy Đủ](#4-danh-sách-services--chi-tiết-đầy-đủ)
5. [Data Architecture — Source of Truth Map](#5-data-architecture--source-of-truth-map)
6. [Cấu Trúc Repo BE](#6-cấu-trúc-repo-be)
7. [Schema Database](#7-schema-database)
8. [Event Contracts](#8-event-contracts)
9. [Business Logic & Luồng Nghiệp Vụ](#9-business-logic--luồng-nghiệp-vụ)
10. [Patterns & Implementation Strategy](#10-patterns--implementation-strategy)
11. [Trade-offs & Production Reasoning](#11-trade-offs--production-reasoning)
12. [AI Integration Strategy](#12-ai-integration-strategy)
13. [API Strategy](#13-api-strategy)
14. [Roadmap BE — 10 Tuần](#14-roadmap-be--10-tuần)
15. [Metrics & Production Dashboards](#15-metrics--production-dashboards)
16. [Portfolio & Interview Prep BE](#16-portfolio--interview-prep-be)
17. [Cut-Scope Guide](#17-cut-scope-guide)
18. [Context Handoff BE](#18-context-handoff-be)
19. [Backend Requirement Compliance Matrix](#19-backend-requirement-compliance-matrix)

---

## 1. Executive Summary

**SignalDesk AI** là nền tảng hỗ trợ khách hàng đa tenant dành cho SaaS/SME, kết hợp:
- ticket & conversation management với full lifecycle
- knowledge base với versioning và publish workflow
- semantic + hybrid search
- agent/admin workspace với role-based access
- AI copilot: RAG chatbot + agent assist + auto-classify
- campaign/follow-up automation
- analytics và observability production-ready

### 1.1 Bài Toán Giải Quyết

SME và SaaS startup thường bị phân mảnh:
- FAQ/knowledge base nằm một nơi, ticket/support nằm nơi khác
- analytics hành vi không kết nối với support
- follow-up/campaign làm thủ công
- AI nếu có thì chỉ là chatbot rời rạc

SignalDesk AI gom thành một platform thống nhất.

### 1.2 Tại Sao Project BE Này Mạnh

- **Multi-tenant SaaS thực sự**: isolation ở DB schema + cache + search + AI context
- **Event-driven với Outbox Pattern**: zero message loss, at-least-once + idempotent = effectively-once
- **RAG pipeline production-grade**: embedding pipeline, vector store, grounding, confidence fallback
- **Observability end-to-end**: trace request xuyên nhiều service với Jaeger/Tempo
- **Concurrency thực chiến**: optimistic locking, Lua distributed lock, Inbox Pattern, cache stampede
- **System thinking rõ ràng**: SLO, error budget, bottleneck analysis, backpressure, scale levers

### 1.3 Demo Tenants

- **TaskFlow** — SaaS quản lý công việc
- **InvoiceFox** — SaaS quản lý billing/invoice

Mỗi tenant: 20–30 KB articles, 50–100 FAQ, 80–150 tickets, 300–800 ticket messages.

---

## 2. Kiến Trúc Hệ Thống

### 2.1 High-Level Architecture

```
                    +-----------------------------------------+
                    |   Next.js Apps (FE Repo — xem FE doc)   |
                    |   portal | workspace | widget-sdk        |
                    +--------------------+--------------------+
                                         |
                    +--------------------v--------------------+
                    |   Gateway BFF (NestJS :3000)            |
                    |   JWT auth, tenant resolve, rate limit  |
                    |   correlation ID, WebSocket endpoint    |
                    +--------+--------+--------+--------------+
                             |        |        |
          ───────────────────┼────────┼────────┼────────────────────
          |           |      |        |        |         |         |
          v           v      v        v        v         v         v
   identity    workspace  support  knowledge  search  notif    ai-svc
   ASP.NET     ASP.NET    ASP.NET  ASP.NET    NestJS  NestJS   NestJS
   :5001       :5002      :5003    :5004      :3002   :3001    :3003
          |           |      |        |
          ─────────────────────────────────
                          |
                    +-----v------+
                    |  RabbitMQ  |
                    |  Event Bus |
                    +-----+------+
                          |
          ────────────────────────────
          |                           |
          v                           v
   campaign-service           (analytics embedded
   ASP.NET :5005              in workspace-service)
```

**Infrastructure:**
- PostgreSQL 16 + **PgBouncer** (connection pooling)
- MongoDB 7, Redis 7, Elasticsearch 8
- MinIO/S3, RabbitMQ 3.13
- OpenTelemetry → Jaeger/Tempo, Prometheus → Grafana, Loki + Promtail
- Nginx (reverse proxy + SSL), GitHub Actions (CI/CD)

### 2.2 Communication Strategy

| Loại | Protocol | Dùng khi nào |
|------|----------|--------------|
| **Sync** | HTTP/REST | Client → Gateway → Service, cần response ngay |
| **Internal Sync** | HTTP (internal DNS) | Service-to-service khi cần data ngay |
| **Async** | RabbitMQ | Notification, AI enrichment, analytics, search indexing |
| **Real-time** | WebSocket (Socket.io) | Live chat, typing indicator, presence, live ticket update |

### 2.3 Tenant Isolation Strategy

```
Database:
  - PostgreSQL multi-schema (identity / workspace / support / knowledge / ops / ...)
  - tenant_id column bắt buộc trên mọi table
  - Application-level filter (không dùng RLS — ORM + ITenantContext dễ hơn)
  - ITenantContext injected vào repositories qua middleware

Cache:
  - Redis key pattern: {entity}:{tenantId}:{id}
  - Không bao giờ cache cross-tenant

Elasticsearch:
  - Single index, mandatory filter: { term: { tenant_id: "uuid" } }
  - Single index > per-tenant index: đơn giản hơn, ít overhead hơn
  - Trade-off: 1 hot tenant có thể ảnh hưởng ES segment — mitigate bằng per-tenant rate limit
    trước khi scale sang per-tenant index

AI:
  - RAG chỉ retrieve articles của tenant hiện tại (bắt buộc trong kNN filter)
  - customer_memory indexed theo (tenant_id, customer_email)
  - System prompt include tenant context

Gateway:
  - tenantId extract từ JWT — client không thể inject tenantId tùy ý
  - Forward qua X-Tenant-Id header
  - Mismatch JWT tenantId vs X-Tenant-Id → reject 403
```

---

## 3. Tech Stack BE & Lý Do Chọn

### 3.1 Backend Services

| Tech | Lý do chọn |
|------|-----------|
| **ASP.NET Core 8** | Core business services — performance, typed domain model, EF Core, MediatR, validation |
| **NestJS** | Gateway, notification, search, AI — nhanh cho integration-heavy, WebSocket, queue consumers |
| **Entity Framework Core 8** | ORM .NET services, migration dễ |
| **MediatR** | CQRS pattern cho .NET services |
| **Prisma / Mongoose** | ORM NestJS services |
| **FluentValidation** | Validation pipeline .NET |
| **Hangfire** | Background jobs, scheduled jobs .NET |
| **BullMQ** | Job queue NestJS |
| **Polly** | Circuit breaker, retry, timeout cho HTTP calls (.NET) |

### 3.2 Infrastructure & Data

| Tech | Lý do chọn |
|------|-----------|
| **PostgreSQL 16** | Core transactional store — ACID, schemas, JSONB, row-level locking |
| **PgBouncer** | Connection pooler trước PostgreSQL. 8 services × pool 10 = ~80–100 connections; PgBouncer giữ actual server connections xuống 20–30. Không có PgBouncer: `max_connections=100` sẽ bị vượt khi load tăng |
| **MongoDB 7** | AI/context bán cấu trúc — ai_runs, customer_memory, conversation_summaries |
| **Redis 7** | Cache, session, rate limiting, presence, idempotency, distributed lock |
| **RabbitMQ** | Message broker — workflow-based messaging, DLQ tốt, dễ vận hành solo |
| **Elasticsearch 8** | Search engine — keyword BM25 + dense_vector kNN hybrid |
| **MinIO** | File storage S3-compatible |
| **Docker + Docker Compose** | Local dev + production deploy |
| **Nginx** | Reverse proxy, SSL termination, gzip |
| **GitHub Actions** | CI/CD |

> **Về Kafka:** Sẽ thêm tuần 11–12 nếu analytics stream cần ordered events hoặc throughput tăng. Không dùng sớm: (1) RabbitMQ đủ dùng cho scale hiện tại; (2) Kafka phức tạp vận hành solo; (3) interviewer sẽ hỏi tại sao cần — phải có benchmark thật.

### 3.3 AI Stack

| Tech | Lý do chọn |
|------|-----------|
| **OpenAI GPT-4o-mini** | LLM primary — nhanh, rẻ, production-ready |
| **Ollama + llama3.2:3b** | LLM fallback tự-host, dev/offline (2GB RAM) |
| **OpenAI text-embedding-3-small** | Embeddings (1536 dims) |
| **nomic-embed-text via Ollama** | Embedding fallback (274MB) |
| **LangChain.js** | RAG orchestration trong ai-service |
| **Elasticsearch dense_vector** | Vector store — không cần Qdrant riêng, giảm complexity |
| **MongoDB** | Customer memory long-term, ai_runs logs |
| **Redis** | Short-term session context cache (TTL 30min) |

### 3.4 Observability

| Tech | Vai trò |
|------|---------|
| **Prometheus** | Metrics scraping |
| **Grafana** | Dashboards + alerts |
| **Jaeger / Tempo** | Distributed tracing (OpenTelemetry) |
| **Loki + Promtail** | Log aggregation |
| **Serilog** | Structured logging (.NET) |
| **Winston** | Structured logging (NestJS) |
| **Sentry** | Error tracking |
| **UptimeRobot** | Uptime monitoring (free) |

---

## 4. Danh Sách Services — Chi Tiết Đầy Đủ

### Application Services — Bảng Tổng Quan

| Service | Tech | Port | Trách nhiệm |
|---------|------|------|-------------|
| `gateway-bff` | NestJS | 3000 | Auth propagation, routing, rate limit, tenant resolution, WebSocket endpoint, correlation ID |
| `identity-service` | ASP.NET Core 8 | 5001 | Register, login, refresh token, email verify |
| `workspace-service` | ASP.NET Core 8 | 5002 | Tenant provisioning, membership, RBAC, feature flags |
| `support-service` | ASP.NET Core 8 | 5003 | Customer profile, ticket lifecycle, SLA, audit trail |
| `knowledge-service` | ASP.NET Core 8 | 5004 | Article CRUD, versioning, publish workflow, file upload |
| `notification-service` | NestJS | 3001 | Email, in-app notification, WebSocket push, DLQ handling |
| `search-service` | NestJS | 3002 | Indexing events, ES sync, unified hybrid search endpoint |
| `ai-service` | NestJS | 3003 | RAG pipeline, suggest reply, summarization, memory, ai_runs |
| `campaign-service` | ASP.NET Core 8 | 5005 | Segment, campaign scheduling, follow-up _(optional, tuần 9)_ |

**Gateway rules:**
- Không nhét business logic vào gateway
- Mọi request gắn `X-Tenant-Id` và `X-Correlation-Id`
- tenantId extract từ JWT — không tin bất kỳ tenantId nào client truyền lên

---

### 4.1 Per-Service Detail

---

#### `gateway-bff` — NestJS :3000

**Vai trò:** Entry point duy nhất cho mọi client (portal, workspace, widget SDK). Xác thực JWT, resolve tenant, inject correlation ID, rate limit, route về downstream services, expose WebSocket endpoint.

**Tech stack:** NestJS, `@nestjs/jwt`, `http-proxy-middleware`, Socket.io, Redis (rate limit + presence)

**Data ownership:** Không own data nghiệp vụ. Chỉ dùng Redis cho:
- Rate limit counters: `ratelimit:{tenantId}:{route}` — token bucket
- WebSocket presence: `presence:user:{userId}` TTL 30s

**Produces events:** Không publish domain events. Chỉ forward request.

**Consumes events:** Không consume từ RabbitMQ trực tiếp.

**Sync dependencies:**
- `identity-service` — validate JWT (hoặc tự verify bằng public key)
- `workspace-service` — resolve tenantId + RBAC nếu cần
- Tất cả downstream services — proxy HTTP

**Async dependencies:** Không có.

**Key behaviors:**
- Mọi request gắn `X-Correlation-Id` (generate nếu client không truyền)
- Mismatch JWT `tenantId` vs bất kỳ request body `tenantId` → reject 403
- WebSocket namespace `/ws` — authenticate khi `connect`, broadcast qua Redis pub/sub adapter

---

#### `identity-service` — ASP.NET Core 8 :5001

**Vai trò:** Quản lý user account — register, login, refresh token, email verification. Không quản lý tenant hay role (đó là workspace-service).

**Tech stack:** ASP.NET Core 8, EF Core 8, MediatR, FluentValidation, BCrypt, JWT (RS256), Hangfire (cleanup jobs)

**Data ownership:**
- `identity.users` — source of truth cho user account
- `identity.refresh_tokens` — source of truth cho active sessions
- `ops.outbox_events` (write side của outbox)

**Produces events:**
- `identity.user.registered.v1` → workspace-service (create initial tenant membership), notification-service (verification email)
- `identity.user.email-verified.v1` → notification-service

**Consumes events:**
- `workspace.member.removed.v1` → revoke tất cả refresh tokens của `userId` với `tenantId` đó (UPDATE identity.refresh_tokens SET revoked_at=NOW() WHERE user_id=:userId AND tenant_id=:tenantId AND revoked_at IS NULL) **[Bug fix v6.0]**

**Sync dependencies:** Không có downstream service deps trong request path.

**Async dependencies:**
- `workspace-service` nhận `identity.user.registered.v1` → auto-create Starter tenant + Admin membership
- `notification-service` nhận `identity.user.registered.v1` → gửi verification email
- `notification-service` nhận `identity.user.email-verified.v1` → gửi confirmation

**Key behaviors:**
- Password hashed với BCrypt cost 12
- JWT: access token 15 phút (RS256), refresh token 30 ngày (hashed)
- Refresh token rotation: mỗi lần refresh tạo token mới + revoke token cũ
- Cleanup job (Hangfire): xóa expired refresh tokens mỗi 6h
- **[Bug fix v6.0] Membership check khi refresh token:** `POST /auth/refresh` phải sync call `workspace-service /internal/memberships?userId=X&tenantId=Y` để verify membership vẫn `Active`. Nếu membership revoked/removed → revoke refresh token ngay + trả 401. Mục đích: ngăn user đã bị remove khỏi tenant tiếp tục dùng refresh token cũ trong window 30 ngày.

---

#### `workspace-service` — ASP.NET Core 8 :5002

**Vai trò:** Tenant lifecycle, membership management, RBAC (roles + permissions), feature flags per tenant, team management.

**Tech stack:** ASP.NET Core 8, EF Core 8, MediatR, FluentValidation

**Data ownership:**
- `workspace.tenants` — source of truth cho tenant
- `workspace.memberships` — source of truth cho user↔tenant relationship
- `workspace.role_permissions`, `workspace.permissions`
- `workspace.feature_flags`

**Produces events:**
- `workspace.tenant.created.v1` → notification-service (welcome)
- `workspace.member.invited.v1` → notification-service (invite email)
- `workspace.member.joined.v1` → không có external consumer ở phase 1; audit log ghi inline cùng transaction
- `workspace.member.removed.v1` → identity-service consumer **[Bug fix v6.0]**: revoke tất cả refresh tokens của user đó trong scope tenantId này

**Consumes events:**
- `identity.user.registered.v1` → auto-create Starter tenant + Admin membership

**Sync dependencies:**
- `identity-service` — validate userId khi invite member (internal HTTP)

**Async dependencies:**
- `notification-service` nhận `tenant.created.v1`, `member.invited.v1`

**Key behaviors:**
- RBAC: permissions cached `user_permissions:{userId}:{tenantId}` TTL 3600s
- Invalidate cache khi role hoặc membership thay đổi
- Feature flags: `feature_flags:{tenantId}` cached TTL 600s — used by all services

---

#### `support-service` — ASP.NET Core 8 :5003

**Vai trò:** Core nghiệp vụ — customer profile, toàn bộ ticket lifecycle (create → assign → reply → resolve → close), SLA calculation, audit trail. Service quan trọng nhất hệ thống.

**Tech stack:** ASP.NET Core 8, EF Core 8, MediatR (CQRS), FluentValidation, Hangfire (SLA breach cron)

**Data ownership:**
- `support.tickets` — **source of truth tuyệt đối cho ticket state**
- `support.ticket_messages`
- `support.ticket_status_history`
- `support.customers`
- `support.sla_policies`
- `support.ticket_counters`
- `ops.outbox_events` (write), `ops.inbox_messages` (read/write),
  `ops.idempotency_keys` (read/write), `ops.audit_logs` (write)

**Produces events:**
- `support.customer.upserted.v1`
- `support.ticket.created.v1`
- `support.ticket.assigned.v1`
- `support.ticket.message-added.v1`
- `support.ticket.resolved.v1`
- `support.ticket.closed.v1`
- `support.ticket.sla-breached.v1`

**Consumes events:**
- `ai.classification.completed.v1` → UPDATE tickets (category, priority, sentiment, tags)
- `ai.summary.completed.v1` → UPDATE tickets (ai_summary)

**Sync dependencies:**
- `workspace-service` — RBAC permission check (internal HTTP, cached)
- `identity-service` — resolve agent profile khi assign (nếu cần display name)

**Async dependencies:**
- `notification-service` — nhận ticket.created, ticket.assigned, ticket.message-added, ticket.resolved, ticket.sla-breached
- `search-service` — nhận ticket.created, ticket.message-added, ticket.resolved, ticket.closed
- `ai-service` — nhận ticket.created (classify), ticket.resolved (summary + memory)
- `campaign-service` — nhận ticket.resolved (trigger follow-up check)

**Key behaviors:**
- ticket_no allocation: `ticket_counters FOR UPDATE` trong cùng transaction
- Optimistic concurrency: `row_version` compare-and-swap trên mọi mutation
- SLA: Hangfire cron mỗi 1 phút check `idx_tickets_sla_breach` partial index
- AI derived fields: category, sentiment, ai_summary — không overwrite manual fields
- Mọi status change → `ops.audit_logs`

---

#### `knowledge-service` — ASP.NET Core 8 :5004

**Vai trò:** Article CRUD với versioning đầy đủ, publish/unpublish workflow, category management, file upload via presigned URL → MinIO.

**Tech stack:** ASP.NET Core 8, EF Core 8, MediatR, FluentValidation, MinIO SDK

**Data ownership:**
- `knowledge.articles` — source of truth cho article metadata + status
- `knowledge.article_versions` — source of truth cho article content (immutable per version)
- `knowledge.categories`
- MinIO bucket `kb-files` — source of truth cho binary attachments

**Produces events:**
- `knowledge.article.published.v1` → search-service (text index + embedding), portal (ISR)
- `knowledge.article.unpublished.v1` → search-service (remove from index)

**Consumes events:** Không consume domain events từ services khác.

**Sync dependencies:**
- `workspace-service` — RBAC check (author/editor/admin)
- MinIO — presigned URL generation (nội bộ)

**Async dependencies:**
- `search-service` nhận `article.published.v1` → ES upsert + embedding job

**Ownership rule:**
- `knowledge-service` chỉ publish article event
- `search-service` là service duy nhất được quyền ghi `kb_articles_v1` (text fields + `dense_vector`)
- `ai-service` chỉ đọc `kb_articles_v1` cho retrieval, không ghi index

**Key behaviors:**
- Publish = snapshot: copy latest draft content → `article_versions` (immutable) + set status=Published
- Versioning: `UNIQUE(article_id, version)` — version auto-increment per article
- File upload: service generate presigned URL → client upload trực tiếp → MinIO → service confirm

---

#### `notification-service` — NestJS :3001

**Vai trò:** Fan-out notification từ domain events. Email (SMTP/SparkPost), in-app notification (MongoDB), WebSocket push (realtime). DLQ handler cho failed notifications.

**Tech stack:** NestJS, BullMQ, Nodemailer/SparkPost SDK, Socket.io (emit via gateway), Mongoose (MongoDB), ioredis

**Data ownership:**
- MongoDB `notifications` collection — in-app notification history per user
- MongoDB `email_templates` collection — Handlebars templates per tenant
- Redis `inbox:{consumer}:{eventId}` NX keys — inbox deduplication (TTL 7 ngày)
- **Không own PostgreSQL tables** — service này không có PG client; inbox dedup dùng Redis thay vì `ops.inbox_messages`

> **[Bug fix v6.0]** `notification-service` không có PostgreSQL client. Inbox dedup chuyển sang Redis:
> `SET inbox:notification:{eventId} 1 NX EX 604800` — NX = chỉ set nếu chưa tồn tại.
> NX fail = đã xử lý → skip. NX success = chạy tiếp. 7 ngày TTL đủ cho window replay an toàn.

**Produces events:** Không publish domain events.

**Consumes events:**
- `identity.user.registered.v1` → verification email
- `workspace.tenant.created.v1` → welcome email
- `workspace.member.invited.v1` → invite email
- `support.ticket.created.v1` → alert email to assigned agent
- `support.ticket.assigned.v1` → notify agent
- `support.ticket.message-added.v1` → email customer nếu `author_type='Agent'` và `is_internal_note=false`
- `support.ticket.resolved.v1` → confirmation email to customer
- `support.ticket.sla-breached.v1` → alert agent/admin
- `identity.user.email-verified.v1` → confirmation

**Sync dependencies:** Không có trong request path (pure async consumer).

**Async dependencies:** Tất cả producers trên.

**Key behaviors:**
- **Inbox dedup (Redis):** `SET inbox:notification:{eventId} 1 NX EX 604800` — skip nếu NX fail
- **Null agent guard `ticket.created.v1`:** `assigned_agent_id` có thể null (widget escalation). Consumer phải check: `IF assigned_agent_id IS NULL → skip agent email` hoặc broadcast tới team/admin queue thay vì resolve email cụ thể. Không guard → NullReferenceException khi resolve agent profile.
- Mọi email có `dedupe_key` (hash của `event_id + recipient_email`) → tránh resend khi replay
- Failed email: retry 3× exponential → DLQ → alert
- In-app: persist MongoDB, emit qua Socket.io room `user:{userId}`
- Template render: Handlebars với tenant branding variables

---

#### `search-service` — NestJS :3002

**Vai trò:** Consume domain events → sync vào Elasticsearch. Expose unified hybrid search endpoint (BM25 + kNN) cho cả tickets và KB articles.

**Tech stack:** NestJS, `@elastic/elasticsearch` client, BullMQ (embedding jobs), ioredis

**Data ownership:**
- Elasticsearch index `tickets_v1` — read model, eventual consistency ≤5s
- Elasticsearch index `kb_articles_v1` — read model bao gồm `dense_vector`
- Redis `inbox:{consumer}:{eventId}` NX keys — inbox deduplication (TTL 7 ngày)
- **Không own PostgreSQL tables** — service này không có PG client; inbox dedup dùng Redis thay vì `ops.inbox_messages`

> **[Bug fix v6.0]** `search-service` không có PostgreSQL client. Inbox dedup chuyển sang Redis giống notification-service.

**Produces events:** Không publish domain events.

**Consumes events:**
- `support.ticket.created.v1` → ES upsert ticket document
- `support.ticket.assigned.v1` → ES update `{ agent_id, aggregate_version }` ← **[Bug fix v6.0] thêm mới — thiếu sẽ khiến `agent_id` trong ES stale sau reassign**
- `support.ticket.message-added.v1` → ES upsert `body_public_plain` nếu `is_internal_note=false`
- `support.ticket.resolved.v1` → ES upsert (update status, resolved_at)
- `support.ticket.closed.v1` → ES upsert (update status, closed_at)
- `ai.summary.completed.v1` → ES upsert `ai_summary` cho ticket document
- `knowledge.article.published.v1` → ES upsert article + schedule embedding job
- `knowledge.article.unpublished.v1` → ES delete document

**Sync dependencies:**
- `knowledge-service` — fetch article body khi index (internal HTTP, nếu payload thiếu body)

**Async dependencies:** BullMQ internal — embedding jobs sau khi text-index article.

**Key behaviors:**
- **Inbox dedup (Redis):** `SET inbox:search:{eventId} 1 NX EX 604800` — skip nếu NX fail
- Stale event guard: ES upsert chỉ apply nếu `aggregate_version` trong event ≥ version hiện tại trong ES (xem Luồng 6 cho atomic Painless script implementation)
- Internal notes KHÔNG được index vào `tickets_v1`; authoritative source vẫn là PostgreSQL ticket detail
- Embedding job: gọi OpenAI embed → upsert `dense_vector` (tách khỏi text index để không block)
- Hybrid search: `knn` + `query.bool.must.match` combined, `_score` sum, filter mandatory `tenant_id`
- Tenant isolation: `term: { tenant_id: "uuid" }` bắt buộc trong mọi query

---

#### `ai-service` — NestJS :3003

**Vai trò:** RAG pipeline (chatbot + agent assist), auto-classify ticket, ticket summarization, long-term customer memory management, ai_runs logging.

**Tech stack:** NestJS, LangChain.js, OpenAI SDK, Ollama (fallback), `@elastic/elasticsearch`, Mongoose (MongoDB), BullMQ, Polly-equivalent (axios-retry + circuit breaker)

**Data ownership:**
- MongoDB `ai_runs` — source of truth cho mọi AI inference log
- MongoDB `chat_sessions` — conversation history per widget session
- MongoDB `customer_memory` — long-term customer profile per `(tenant_id, email)`
- MongoDB `inbox_messages` collection — inbox deduplication per event (`{ consumer_name, event_id }` unique index)
- Redis `ai_context:{sessionId}` — short-term context TTL 30min (volatile, not source of truth)
- Elasticsearch `kb_articles_v1` — read only (retrieve, không write)

> **[Bug fix v6.0]** `ai-service` không có PostgreSQL client. Inbox dedup dùng MongoDB thay vì `ops.inbox_messages`:
> `db.inbox_messages.insertOne({ consumer_name, event_id })` với unique index `(consumer_name, event_id)`.
> Duplicate key error = đã xử lý → skip. TTL index `created_at` với expiry 30 ngày tự cleanup.

**Produces events:**
- `ai.classification.completed.v1` → support-service (update ticket fields)
- `ai.summary.completed.v1` → support-service (update ai_summary)

**Consumes events:**
- `support.ticket.created.v1` → auto-classify (category, priority, sentiment, tags)
- `support.ticket.resolved.v1` → summarize + update customer_memory

**Sync dependencies (HTTP trong request path):**
- `support-service` — fetch ticket content khi classify/summarize
- Elasticsearch — kNN hybrid retrieve
- OpenAI API / Ollama — LLM + embedding calls

**Async dependencies:**
- BullMQ internal — summary / suggestion / non-request AI jobs
- Circuit breaker → fallback: Ollama → human handoff signal

**Key behaviors:**
- Confidence threshold 0.75: dưới ngưỡng → escalate, không hallucinate
- Field ownership: chỉ write `category`, `sentiment`, `tags`, `ai_summary` — không động vào manual fields
- Mọi inference → `ai_runs` MongoDB (model, prompt_tokens, latency, confidence, tenant_id)
- Backpressure: prefetch 5 + per-tenant rate limit 20 AI jobs/min/tenant + circuit breaker

---

#### `campaign-service` — ASP.NET Core 8 :5005 _(optional, tuần 9)_

**Vai trò:** Định nghĩa campaign/segment, schedule follow-up sau ticket resolved, throttle dispatch, track delivery status.

**Tech stack:** ASP.NET Core 8, EF Core 8, Hangfire (scheduled jobs), MediatR

**Data ownership:**
- `campaign.campaigns` — campaign definitions
- `campaign.campaign_dispatches` — dispatch log với UNIQUE(campaign_id, ticket_id)
- `campaign.segments` — audience segment rules

**Produces events:**
- `campaign.dispatch.requested.v1` → notification-service (gửi email/in-app)

**Consumes events:**
- `support.ticket.resolved.v1` → check campaign triggers → schedule follow-up job

**Sync dependencies:**
- `support-service` — fetch customer segment data

**Async dependencies:**
- `notification-service` nhận `campaign.dispatch.requested.v1`

**Key behaviors:**
- Phase 1 semantics: 1 follow-up tối đa / (campaign, resolved ticket)
- Idempotency: `UNIQUE(campaign_id, ticket_id)` — replay safe
- Per-campaign advisory lock: `SET campaign:lock:{campaignId}:{ticketId} {instanceId}:{token} NX PX 600000`
- Throttle: 50 emails/s per tenant (Hangfire job batch + sleep)
- Crash recovery: Hangfire persistent jobs — restart từ checkpoint

### Infrastructure Services

| Service | Port | Notes |
|---------|------|-------|
| PostgreSQL 16 | 5432 | apps connect qua PgBouncer, không direct |
| PgBouncer | 5433 | transaction pooling, max_pool=25 |
| MongoDB 7 | 27017 | ai-service, notification-service |
| Redis 7 | 6379 | tất cả services |
| RabbitMQ 3.13 | 5672 / 15672 (UI) | event bus |
| Elasticsearch 8 | 9200 | search-service, ai-service |
| MinIO | 9000 / 9001 (console) | knowledge-service |
| Prometheus | 9090 | |
| Grafana | 3100 | |
| Jaeger / Tempo | 16686 / 3200 | |
| Loki | 3300 | |
| Mailpit | 1025 / 8025 (UI) | dev email |
| Ollama | 11434 | ai-service fallback |

---

## 5. Data Architecture — Source of Truth Map

> **Câu hỏi phỏng vấn hay nhất:** "Source of truth của ticket là gì? Khi nào Elasticsearch đúng? Khi nào Redis tin được?" — Section này trả lời trực tiếp.

---

### 5.1 Storage Roles

| Store | Dùng cho | Source of truth? | Consistency | TTL / Retention |
|-------|----------|-----------------|-------------|-----------------|
| **PostgreSQL** | Tickets, users, KB metadata, tenant, workspace, SLA, audit, outbox, inbox | ✅ YES — mọi write committed ở đây trước | Strong ACID | Permanent |
| **MongoDB** | ai_runs, chat_sessions, customer_memory, email_templates, in-app notifications | ✅ YES — cho AI domain data (bán cấu trúc, schema-free) | Eventual (replica) | ai_runs: 90 ngày; memory: permanent |
| **Redis** | Cache (ticket, KB, permissions), session, rate limiter, distributed lock, presence, pub/sub | ❌ Projection / volatile | Volatile — TTL-based | TTL 30s → 3600s |
| **Elasticsearch** | Full-text + semantic search (tickets + KB), dense_vector kNN | ❌ Read model — eventual consistency | Eventual ≤ 5s from PostgreSQL/MongoDB | Permanent (index alias) |
| **MinIO / S3** | Binary files: KB article attachments, uploaded images, exports | ✅ YES — binary blob store | Strong (object storage) | Policy per bucket |

---

### 5.2 Source of Truth Per Domain

```
Ticket state (status, assignment, messages, SLA):
  → PostgreSQL support.tickets + support.ticket_messages
  → Luôn đọc PostgreSQL cho ticket detail (read-your-writes)
  → Elasticsearch = search/list (stale ≤ 5s — acceptable)

User account (email, password_hash, sessions):
  → PostgreSQL identity.users + identity.refresh_tokens

Tenant / RBAC:
  → PostgreSQL workspace.tenants + workspace.memberships + workspace.role_permissions
  → Redis cache user_permissions:{userId}:{tenantId} TTL 3600s — invalidate on role change

KB article content:
  → PostgreSQL knowledge.article_versions (immutable snapshots per version)
  → Elasticsearch kb_articles_v1 = search + vector retrieval (read model, maintained only by search-service)
  → MinIO = binary attachments

AI inference history:
  → MongoDB ai_runs (immutable append-only per inference)

Customer memory (long-term AI context):
  → MongoDB customer_memory (upsert per tenant+email)
  → Redis ai_context:{sessionId} = short-term within 1 session (volatile)

Search results:
  → Elasticsearch = read model (không bao giờ là source of truth)
  → Khi ES stale: user thấy danh sách cũ — acceptable, ticket detail vẫn đúng từ PG
```

---

### 5.3 Projection & Eventual Consistency Map

```
Write (PostgreSQL commit)
  │
  ├──→ outbox_events published → RabbitMQ
  │         │
  │         ├──→ search-service consumer → ES upsert
  │         │     Freshness target: ≤ 5s
  │         │     Staleness acceptable: YES (search, list)
  │         │     Staleness NOT acceptable: ticket detail → đọc PG trực tiếp
  │         │
  │         ├──→ ai-service consumer → MongoDB ai_runs, customer_memory
  │         │     Freshness target: ≤ 30s (async enrichment)
  │         │     Staleness acceptable: YES (AI enrichment)
  │         │
  │         ├──→ notification-service → email/in-app
  │         │     Freshness target: ≤ 60s (email)
  │         │     Staleness acceptable: YES (notification = side effect)
  │         │
  │         └──→ campaign-service → schedule job
  │               Freshness target: ≤ 2 phút (job scheduling)
  │
  └──→ Redis cache eviction (inline, synchronous với write handler)
        Đảm bảo: read-your-writes cho requester ngay sau write
```

---

### 5.4 Read-Your-Writes Strategy

**Ticket detail:** Luôn đọc PostgreSQL — không bao giờ đọc từ ES cho `/tickets/{id}`.

**Ticket list/search:** Đọc Elasticsearch — stale ≤ 5s acceptable.

**Permissions:** Đọc Redis cache (TTL 3600s) — invalidate ngay khi membership thay đổi.

**KB article (public portal):** Đọc ES hoặc Next.js ISR cache — eventual consistent.

**KB article (editor view):** Đọc PostgreSQL `article_versions` — luôn authoritative.

**AI results:** Không có read-your-writes requirement — AI enrichment là async side effect.

---

## 6. Cấu Trúc Repo BE

```
signaldesk-be/              ← NX monorepo
├── services/
│   ├── gateway-bff/        ← NestJS
│   │   └── src/
│   │       ├── middleware/
│   │       │   ├── auth.middleware.ts        ← JWT verify + tenant extract
│   │       │   ├── rate-limit.middleware.ts  ← Redis token bucket per tenant
│   │       │   └── correlation.middleware.ts
│   │       ├── proxy/
│   │       │   ├── ticket.proxy.ts
│   │       │   ├── knowledge.proxy.ts
│   │       │   └── ai.proxy.ts
│   │       ├── guards/
│   │       └── websocket/
│   │
│   ├── identity-service/   ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── IdentityService.Domain/
│   │       │   ├── Entities/    (User, RefreshToken)
│   │       │   └── Events/      (UserRegisteredEvent)
│   │       ├── IdentityService.Application/
│   │       │   ├── Commands/    (RegisterUser, Login, RefreshToken)
│   │       │   └── Queries/
│   │       ├── IdentityService.Infrastructure/
│   │       │   ├── Persistence/ (EF Core, Migrations)
│   │       │   └── Messaging/   (OutboxPublisher)
│   │       └── IdentityService.API/
│   │
│   ├── workspace-service/  ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── WorkspaceService.Domain/
│   │       │   ├── Entities/    (Tenant, Membership, Role, Permission, FeatureFlag)
│   │       │   └── Events/      (TenantCreated, MemberInvited)
│   │       ├── WorkspaceService.Application/
│   │       ├── WorkspaceService.Infrastructure/
│   │       └── WorkspaceService.API/
│   │
│   ├── support-service/    ← ASP.NET Core 8 (Clean Architecture + CQRS)
│   │   └── src/
│   │       ├── SupportService.Domain/
│   │       │   ├── Entities/      (Customer, Ticket, TicketMessage, SlaPolicy, Team)
│   │       │   ├── ValueObjects/  (TicketStatus, Priority)
│   │       │   └── Events/        (TicketCreated, Assigned, Resolved)
│   │       ├── SupportService.Application/
│   │       │   ├── Commands/      (CreateTicket, AssignTicket, AddMessage, Resolve)
│   │       │   ├── Queries/       (GetTickets, GetTicketById, GetTicketStats)
│   │       │   └── Handlers/
│   │       ├── SupportService.Infrastructure/
│   │       │   ├── Persistence/   (AppDbContext, Repositories, Migrations)
│   │       │   └── Messaging/     (OutboxPublisher, RabbitMQ producer)
│   │       └── SupportService.API/
│   │           └── Controllers/   (TicketsController, CustomersController)
│   │
│   ├── knowledge-service/  ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── KnowledgeService.Domain/
│   │       │   ├── Entities/    (Article, Category, ArticleVersion)
│   │       │   └── Events/      (ArticlePublished)
│   │       ├── KnowledgeService.Application/
│   │       ├── KnowledgeService.Infrastructure/
│   │       └── KnowledgeService.API/
│   │
│   ├── notification-service/ ← NestJS
│   │   └── src/
│   │       ├── consumers/      (ticket-events, campaign-events)
│   │       ├── transports/     (email.transport.ts, inapp.transport.ts)
│   │       └── providers/      (template.service.ts — MongoDB templates)
│   │
│   ├── search-service/     ← NestJS
│   │   └── src/
│   │       ├── consumers/      (article-indexer, ticket-indexer)
│   │       ├── indexing/       (elasticsearch.service.ts)
│   │       └── retrieval/      (hybrid BM25 + kNN search)
│   │
│   ├── ai-service/         ← NestJS
│   │   └── src/
│   │       ├── rag/            (rag.service.ts, retriever.service.ts)
│   │       ├── classify/       (intent.service.ts)
│   │       ├── suggest/        (suggest.service.ts)
│   │       ├── memory/         (memory.service.ts — MongoDB long-term)
│   │       └── consumers/      (ticket-created, article-published)
│   │
│   └── campaign-service/   ← ASP.NET Core 8 (optional)
│       └── src/
│           ├── CampaignService.Domain/
│           └── CampaignService.Application/
│               └── Jobs/  (Hangfire scheduled jobs)
│
├── libs/
│   ├── contracts/events/           ← Event schemas (JSON Schema / TypeScript)
│   ├── dotnet/BuildingBlocks/
│   │   ├── Common/                 ← Result<T>, base Entity, ITenantContext
│   │   ├── Messaging/              ← RabbitMQ producer abstractions
│   │   ├── Persistence/            ← Outbox base, soft delete, audit
│   │   └── Observability/          ← OTel setup, structured logging
│   └── node/common/
│       ├── config/
│       ├── logger/                 ← Winston + OTel
│       ├── messaging/              ← RabbitMQ client helpers
│       └── tracing/
│
├── infra/
│   ├── docker/
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.dev.yml
│   │   └── docker-compose.infra.yml
│   ├── nginx/
│   └── observability/
│       ├── prometheus/prometheus.yml
│       ├── grafana/dashboards/
│       ├── loki/loki-config.yaml
│       └── otel-collector/otel-config.yaml
│
└── .github/workflows/
    ├── ci.yml
    └── deploy-production.yml
```

---

## 7. Schema Database

### PostgreSQL Strategy

Một PostgreSQL instance `signaldesk`, tách theo schema để giữ logical boundary theo service.
Apps kết nối qua **PgBouncer :5433** (transaction pooling, max_pool=25 per database).

**Schemas trong PostgreSQL:** `identity`, `workspace`, `support`, `knowledge`, `campaign`, `ops`

**Lưu ý:** `notification-service` không có PostgreSQL schema ở phase này.
Notification persistence nằm ở MongoDB collections `notifications` và `email_templates`.

**Cross-schema FK policy (phase 1 pragmatic trade-off):**
- Cho phép cross-schema FK khi services vẫn cùng một PostgreSQL instance
  (ví dụ: `workspace.memberships.user_id -> identity.users.id`)
- Đây là quyết định vận hành thực dụng cho portfolio/demo scale, không phải hard requirement của microservices
- Khi tách `identity-service` sang DB riêng: thay FK bằng application-level validation + async read model/user snapshot

---

### Schema `identity`

```sql
CREATE TABLE identity.users (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email           VARCHAR(255) NOT NULL UNIQUE,
  password_hash   VARCHAR(255) NOT NULL,
  display_name    VARCHAR(255) NOT NULL,
  avatar_url      VARCHAR(500),
  status          VARCHAR(50)  NOT NULL DEFAULT 'Pending',
  email_verified  BOOLEAN NOT NULL DEFAULT false,
  last_login_at   TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE identity.refresh_tokens (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
  tenant_id   UUID,                         -- [Bug fix v6.0] nullable: null = global session; non-null = scoped to tenant for per-tenant revocation
  token_hash  VARCHAR(255) NOT NULL UNIQUE,
  device_id   VARCHAR(255),
  expires_at  TIMESTAMPTZ NOT NULL,
  revoked_at  TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_user    ON identity.refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_expires ON identity.refresh_tokens(expires_at)
  WHERE revoked_at IS NULL;                  -- partial: chỉ active tokens
CREATE INDEX idx_refresh_tokens_user_tenant ON identity.refresh_tokens(user_id, tenant_id)
  WHERE revoked_at IS NULL;                  -- [Bug fix v6.0] cho query revoke per-tenant
```

---

### Schema `workspace`

```sql
CREATE TABLE workspace.tenants (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  slug        VARCHAR(100) NOT NULL UNIQUE,
  name        VARCHAR(255) NOT NULL,
  plan_code   VARCHAR(50)  NOT NULL DEFAULT 'Starter',
  status      VARCHAR(50)  NOT NULL DEFAULT 'Trial',
  settings    JSONB DEFAULT '{}',
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE workspace.memberships (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL REFERENCES workspace.tenants(id) ON DELETE CASCADE,
  user_id     UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
  role_code   VARCHAR(50) NOT NULL,
  status      VARCHAR(50) NOT NULL DEFAULT 'Invited',
  invited_by  UUID REFERENCES identity.users(id),
  joined_at   TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, user_id)
);

CREATE TABLE workspace.permissions (
  id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  action  VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE workspace.role_permissions (
  tenant_id     UUID NOT NULL REFERENCES workspace.tenants(id),
  role_code     VARCHAR(50) NOT NULL,
  permission_id UUID NOT NULL REFERENCES workspace.permissions(id),
  PRIMARY KEY (tenant_id, role_code, permission_id)
);

CREATE TABLE workspace.feature_flags (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL REFERENCES workspace.tenants(id),
  flag_key    VARCHAR(100) NOT NULL,
  enabled     BOOLEAN NOT NULL DEFAULT false,
  config      JSONB DEFAULT '{}',
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, flag_key)
);

-- Indexes
CREATE INDEX idx_memberships_tenant        ON workspace.memberships(tenant_id);
CREATE INDEX idx_memberships_user          ON workspace.memberships(user_id);
CREATE INDEX idx_memberships_tenant_status ON workspace.memberships(tenant_id, status);
CREATE INDEX idx_feature_flags_tenant      ON workspace.feature_flags(tenant_id);
```

---

### Schema `support`

```sql
CREATE TABLE support.customers (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    UUID NOT NULL,
  external_ref VARCHAR(255),
  email        VARCHAR(255) NOT NULL,
  name         VARCHAR(255),
  phone        VARCHAR(50),
  tags         TEXT[] DEFAULT '{}',
  metadata     JSONB DEFAULT '{}',
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, email)
);

CREATE TABLE support.tickets (
  id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id          UUID NOT NULL,
  ticket_no          VARCHAR(20) NOT NULL,
  customer_id        UUID NOT NULL REFERENCES support.customers(id),
  subject            TEXT NOT NULL,
  status             VARCHAR(50) NOT NULL DEFAULT 'New',
  priority           VARCHAR(50) NOT NULL DEFAULT 'Medium',
  channel            VARCHAR(50) NOT NULL DEFAULT 'Widget',
  category           VARCHAR(100),
  sentiment          VARCHAR(50),
  tags               TEXT[] DEFAULT '{}',
  assigned_agent_id  UUID,
  team_id            UUID,
  first_response_at  TIMESTAMPTZ,
  resolved_at        TIMESTAMPTZ,
  closed_at          TIMESTAMPTZ,
  sla_deadline       TIMESTAMPTZ,
  sla_met            BOOLEAN,
  ai_summary         TEXT,
  row_version        INT NOT NULL DEFAULT 1,   -- optimistic concurrency
  deleted_at         TIMESTAMPTZ,              -- [Bug fix v6.0] soft delete: NULL = active, non-NULL = deleted
  created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, ticket_no)
);

CREATE TABLE support.ticket_messages (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  ticket_id        UUID NOT NULL REFERENCES support.tickets(id) ON DELETE CASCADE,
  tenant_id        UUID NOT NULL,
  author_type      VARCHAR(20) NOT NULL,   -- Customer | Agent | System | AI
  author_id        UUID,
  body_html        TEXT NOT NULL,
  body_plain       TEXT NOT NULL,
  is_internal_note BOOLEAN NOT NULL DEFAULT false,
  attachments      JSONB DEFAULT '[]',
  created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE support.ticket_status_history (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  ticket_id   UUID NOT NULL REFERENCES support.tickets(id),
  tenant_id   UUID NOT NULL,
  from_status VARCHAR(50),
  to_status   VARCHAR(50) NOT NULL,
  changed_by  UUID,
  reason      TEXT,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE support.sla_policies (
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           UUID NOT NULL,
  name                VARCHAR(100) NOT NULL,
  priority            VARCHAR(50) NOT NULL,
  first_response_mins INT NOT NULL,
  resolution_mins     INT NOT NULL,
  is_default          BOOLEAN NOT NULL DEFAULT false,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE support.ticket_counters (
  tenant_id  UUID PRIMARY KEY,
  last_value BIGINT NOT NULL DEFAULT 0,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ================================================================
-- COMPOSITE INDEXES — Critical cho multi-tenant query pattern
-- ================================================================

-- Ticket list: filter tenant + status (most common)
-- tenant_id đứng đầu vì mọi query đều scope tenant trước
CREATE INDEX idx_tickets_tenant_status
  ON support.tickets(tenant_id, status)
  WHERE deleted_at IS NULL;              -- [Bug fix v6.0] exclude soft-deleted rows

-- Ticket list: agent's own queue
CREATE INDEX idx_tickets_tenant_agent
  ON support.tickets(tenant_id, assigned_agent_id)
  WHERE assigned_agent_id IS NOT NULL
    AND deleted_at IS NULL;             -- [Bug fix v6.0]

-- Ticket list: sort mới nhất per tenant
CREATE INDEX idx_tickets_tenant_created
  ON support.tickets(tenant_id, created_at DESC)
  WHERE deleted_at IS NULL;             -- [Bug fix v6.0]

-- SLA breach cron: tìm tickets quá hạn chưa resolve
-- Partial index giảm size index đáng kể (chỉ open tickets)
CREATE INDEX idx_tickets_sla_breach
  ON support.tickets(tenant_id, sla_deadline)
  WHERE status NOT IN ('Resolved', 'Closed');

-- Messages: full timeline của 1 ticket
CREATE INDEX idx_ticket_messages_ticket
  ON support.ticket_messages(ticket_id, created_at ASC);

-- Messages: tenant isolation
CREATE INDEX idx_ticket_messages_tenant
  ON support.ticket_messages(tenant_id);

-- Customers: lookup by email per tenant
CREATE INDEX idx_customers_tenant_email
  ON support.customers(tenant_id, email);
```

> **Index strategy khi phỏng vấn:** "Composite index `(tenant_id, status)` đặt `tenant_id` trước vì mọi query đều filter tenant đầu tiên — PostgreSQL narrow scope xuống tenant đó rồi mới filter status, tránh full scan. Partial index trên `sla_deadline WHERE status NOT IN (...)` giảm kích thước index xuống chỉ còn open tickets — SLA cron job chạy nhanh hơn đáng kể. Verify bằng `EXPLAIN ANALYZE`, không index blindly."

---

### Schema `knowledge`

```sql
CREATE TABLE knowledge.categories (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    UUID NOT NULL,
  parent_id    UUID REFERENCES knowledge.categories(id),
  slug         VARCHAR(255) NOT NULL,
  name         VARCHAR(255) NOT NULL,
  sort_order   INT NOT NULL DEFAULT 0,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, slug)
);

CREATE TABLE knowledge.articles (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    UUID NOT NULL,
  category_id  UUID REFERENCES knowledge.categories(id),
  slug         VARCHAR(255) NOT NULL,
  title        VARCHAR(500) NOT NULL,
  summary      TEXT,
  status       VARCHAR(50) NOT NULL DEFAULT 'Draft',
  visibility   VARCHAR(50) NOT NULL DEFAULT 'Public',
  published_at TIMESTAMPTZ,
  created_by   UUID NOT NULL,
  updated_by   UUID,
  deleted_at   TIMESTAMPTZ,              -- [Bug fix v6.0] soft delete: NULL = active, non-NULL = deleted
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, slug)
);

CREATE TABLE knowledge.article_versions (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  article_id     UUID NOT NULL REFERENCES knowledge.articles(id) ON DELETE CASCADE,
  version        INT NOT NULL,
  body_markdown  TEXT NOT NULL,
  body_html      TEXT NOT NULL,
  body_plain     TEXT NOT NULL,
  change_summary VARCHAR(500),
  created_by     UUID NOT NULL,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(article_id, version)
);

CREATE INDEX idx_articles_tenant_status
  ON knowledge.articles(tenant_id, status)
  WHERE deleted_at IS NULL;              -- [Bug fix v6.0] exclude soft-deleted
CREATE INDEX idx_articles_tenant_category
  ON knowledge.articles(tenant_id, category_id)
  WHERE deleted_at IS NULL;             -- [Bug fix v6.0]
CREATE INDEX idx_categories_tenant_parent
  ON knowledge.categories(tenant_id, parent_id);
CREATE INDEX idx_article_versions_article
  ON knowledge.article_versions(article_id, version DESC);
```

---

### Schema `campaign`

```sql
CREATE TABLE campaign.segments (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id      UUID NOT NULL,
  name           VARCHAR(255) NOT NULL,
  rule_json      JSONB NOT NULL DEFAULT '{}',
  created_by     UUID NOT NULL,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE campaign.campaigns (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id      UUID NOT NULL,
  segment_id     UUID REFERENCES campaign.segments(id),
  name           VARCHAR(255) NOT NULL,
  trigger_type   VARCHAR(50) NOT NULL DEFAULT 'ticket_resolved',   -- phase 1: ticket_resolved only
  delay_minutes  INT NOT NULL DEFAULT 1440,
  template_id    VARCHAR(100) NOT NULL,
  status         VARCHAR(50) NOT NULL DEFAULT 'Draft',
  created_by     UUID NOT NULL,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE campaign.campaign_dispatches (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id     UUID NOT NULL REFERENCES campaign.campaigns(id) ON DELETE CASCADE,
  ticket_id       UUID NOT NULL,
  recipient_id    UUID NOT NULL,
  recipient_email VARCHAR(255) NOT NULL,
  status          VARCHAR(50) NOT NULL DEFAULT 'Scheduled',
  scheduled_for   TIMESTAMPTZ NOT NULL,
  sent_at         TIMESTAMPTZ,
  last_error      TEXT,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(campaign_id, ticket_id)
);

CREATE INDEX idx_campaigns_tenant_trigger
  ON campaign.campaigns(tenant_id, trigger_type, status);
CREATE INDEX idx_dispatches_campaign_status
  ON campaign.campaign_dispatches(campaign_id, status, scheduled_for);
CREATE INDEX idx_dispatches_recipient
  ON campaign.campaign_dispatches(recipient_email);
```

---

### Schema `ops`

```sql
CREATE TABLE ops.outbox_events (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  service_name   VARCHAR(100) NOT NULL,
  aggregate_type VARCHAR(100) NOT NULL,
  aggregate_id   UUID NOT NULL,
  event_type     VARCHAR(200) NOT NULL,
  payload        JSONB NOT NULL,
  headers        JSONB NOT NULL DEFAULT '{}',
  status         VARCHAR(50) NOT NULL DEFAULT 'pending',
  retry_count    INT NOT NULL DEFAULT 0,
  next_retry_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  claimed_by     VARCHAR(200),
  claimed_at     TIMESTAMPTZ,
  last_error     TEXT,
  occurred_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  published_at   TIMESTAMPTZ
);

CREATE TABLE ops.inbox_messages (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  consumer_name VARCHAR(100) NOT NULL,
  event_id      UUID NOT NULL,
  status        VARCHAR(50) NOT NULL DEFAULT 'Processing',
  processed_at  TIMESTAMPTZ,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(consumer_name, event_id)
);

CREATE TABLE ops.idempotency_keys (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id        UUID NOT NULL,
  scope            VARCHAR(100) NOT NULL,      -- ticket.create | ticket.add-message
  idempotency_key  VARCHAR(255) NOT NULL,
  request_hash     CHAR(64) NOT NULL,          -- SHA-256 của canonical request payload
  status           VARCHAR(50) NOT NULL DEFAULT 'Processing',
  locked_until     TIMESTAMPTZ NOT NULL,
  response_status  INT,
  response_body    JSONB,
  resource_type    VARCHAR(100),               -- ticket | ticket_message
  resource_id      UUID,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  completed_at     TIMESTAMPTZ,
  expires_at       TIMESTAMPTZ NOT NULL,
  UNIQUE(tenant_id, scope, idempotency_key)
);

CREATE TABLE ops.audit_logs (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id     UUID NOT NULL,
  actor_id      UUID,
  actor_type    VARCHAR(50) NOT NULL,
  action        VARCHAR(200) NOT NULL,
  resource_type VARCHAR(100) NOT NULL,
  resource_id   UUID,
  before_state  JSONB,
  after_state   JSONB,
  ip_address    VARCHAR(45),
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- [Bug fix v6.0] Partial index: chỉ pending/processing rows, không index đã published
-- Dùng composite (service_name, status, next_retry_at) thay vì (status, next_retry_at)
-- để OutboxPublisher từng service không scan events của service khác
CREATE INDEX idx_outbox_pending_service
  ON ops.outbox_events(service_name, status, next_retry_at)
  WHERE status IN ('pending', 'processing');

-- [Bug fix v6.0] Index cho stale reclaim query: WHERE status='processing' AND claimed_at < ...
-- Thiếu index này → sequential scan toàn bộ processing rows mỗi 2 phút
CREATE INDEX idx_outbox_stale_reclaim
  ON ops.outbox_events(claimed_at)
  WHERE status = 'processing';

CREATE INDEX idx_idempotency_expires
  ON ops.idempotency_keys(expires_at);

CREATE INDEX idx_audit_tenant_created
  ON ops.audit_logs(tenant_id, created_at DESC);

-- [Bug fix v6.0] ops.inbox_messages retention policy
-- Không có cleanup → table tích lũy hàng triệu rows sau vài tháng production
-- Cleanup job (Hangfire cron, daily 03:00):
--   DELETE FROM ops.inbox_messages
--   WHERE status = 'Done' AND created_at < NOW() - INTERVAL '30 days';
-- Lý do giữ 30 ngày: đủ cho replay window nếu cần re-process events cũ
```

---

### Elasticsearch Index Mappings

```json
// tickets_v1
{
  "mappings": {
    "properties": {
      "ticket_id":          { "type": "keyword" },
      "tenant_id":          { "type": "keyword" },
      "ticket_no":          { "type": "keyword" },
      "subject":            { "type": "text", "boost": 2.0 },
      "body_public_plain":  { "type": "text" },
      "status":             { "type": "keyword" },
      "priority":           { "type": "keyword" },
      "category":           { "type": "keyword" },
      "tags":               { "type": "keyword" },
      "agent_id":           { "type": "keyword" },
      "aggregate_version":  { "type": "integer" },
      "created_at":         { "type": "date" }
    }
  }
}

// kb_articles_v1
{
  "mappings": {
    "properties": {
      "article_id":   { "type": "keyword" },
      "tenant_id":    { "type": "keyword" },
      "title":        { "type": "text", "boost": 2.0 },
      "summary":      { "type": "text" },
      "body_plain":   { "type": "text" },
      "category":     { "type": "keyword" },
      "tags":         { "type": "keyword" },
      "status":       { "type": "keyword" },
      "visibility":   { "type": "keyword" },
      "embedding": {
        "type": "dense_vector",
        "dims": 1536,
        "index": true,
        "similarity": "cosine"
      },
      "published_at": { "type": "date" }
    }
  }
}
```

> **`num_candidates: 50` khi phỏng vấn:** "kNN approximate search: `k=5` là số kết quả muốn lấy, `num_candidates=50` là candidates ES xem xét trước khi rerank. Tăng → precision cao hơn nhưng latency tăng. 50 là sweet spot cho corpus vài nghìn articles per tenant. Nếu corpus lớn hơn 100k docs thì benchmark với 100–200."

---

## 8. Event Contracts

### 8.1 Event Envelope Chuẩn

```json
{
  "eventId":       "uuid",
  "eventType":     "support.ticket.created.v1",
  "occurredAt":    "ISO8601",
  "producer":      "support-service",
  "tenantId":      "uuid",
  "correlationId": "uuid",
  "causationId":   "uuid",
  "aggregate":     { "type": "ticket", "id": "uuid", "version": 1 },
  "actor":         { "type": "user", "id": "uuid" },
  "payload":       {}
}
```

### 8.2 Domain Events

> **Canonical rule:** Bảng dưới đây là danh sách domain events chuẩn của hệ thống. Mọi service detail ở Section 4 phải khớp với matrix này; nếu khác nhau thì matrix này thắng.

| Event | Producer | Consumers |
|-------|----------|-----------|
| `identity.user.registered.v1` | identity-svc | workspace-svc, notification |
| `identity.user.email-verified.v1` | identity-svc | notification |
| `workspace.tenant.created.v1` | workspace-svc | notification |
| `workspace.member.invited.v1` | workspace-svc | notification |
| `workspace.member.joined.v1` | workspace-svc | none (phase 1; audit inline) |
| `support.customer.upserted.v1` | support-svc | none (phase 1; reserved for customer projection/segments) |
| `support.ticket.created.v1` | support-svc | notification, search, ai-service |
| `support.ticket.assigned.v1` | support-svc | notification |
| `support.ticket.message-added.v1` | support-svc | search, notification |
| `support.ticket.resolved.v1` | support-svc | notification, ai-service, campaign, search |
| `support.ticket.closed.v1` | support-svc | search |
| `support.ticket.sla-breached.v1` | support-svc | notification |
| `knowledge.article.published.v1` | knowledge-svc | search (text index + embed), portal (ISR) |
| `knowledge.article.unpublished.v1` | knowledge-svc | search |
| `ai.classification.completed.v1` | ai-svc | support-svc (update ticket fields) |
| `ai.summary.completed.v1` | ai-svc | support-svc, search |

`support.ticket.message-added.v1` payload tối thiểu phải có:
- `messageId`, `ticketId`, `tenantId`, `authorType`, `isInternalNote`, `bodyPlain`, `customerEmail`
- `notification-service` chỉ gửi email nếu `authorType='Agent'` và `isInternalNote=false`
- `search-service` chỉ append vào `tickets_v1.body_public_plain` nếu `isInternalNote=false`
- Internal note vẫn chỉ đọc từ PostgreSQL ticket detail, không đi vào search/AI retrieval ở phase 1

### 8.3 Job Messages

Job message = point-to-point command cho đúng một worker/queue xử lý.
Khác với domain event: không fan-out broadcast, và thường represent "hãy làm việc này".

| Job message | Producer | Target queue | Consumer | Purpose |
|-------------|----------|--------------|----------|---------|
| `notification.send-email.v1` | notification-svc | `notification-email` | notification-worker | gửi 1 email cụ thể tới recipient |
| `search.embed-article.v1` | search-svc | `search-embed` | search-worker | tạo embedding cho article rồi upsert `dense_vector` |
| `ai.generate-suggest-reply.v1` | ai-svc | `ai-tasks` | ai-worker | generate reply draft ngoài request path nếu cần async |
| `ai.generate-summary.v1` | ai-svc | `ai-tasks` | ai-worker | summarize ticket thread sau resolve/assign |
| `campaign.dispatch.requested.v1` | campaign-svc | `notification-campaign` | notification-worker | render template + gửi follow-up campaign |

Rules cho job messages:
- định tuyến vào dedicated queue, không broadcast nhiều consumers
- payload phải đủ để worker chạy độc lập, không phụ thuộc state tạm trong memory
- retry / DLQ / dedupe áp dụng giống event consumer

### 8.4 Fan-out Cost Analysis

`support.ticket.resolved.v1` fan-out ra **4 consumers**. Cost analysis phải biết khi phỏng vấn:

```
Consumer              Latency est.    Side effect risk & mitigation
─────────────────────────────────────────────────────────────────────────────
notification-svc      ~100ms (email)  Email 2 lần nếu replay → dedupe_key per recipient
ai-svc (memory)       ~2–5s (LLM)    Customer memory update → idempotent (upsert by tenant+email)
campaign-svc          ~50ms           Schedule check → idempotency key per (campaign_id, ticket_id)
search-svc            ~200ms (ES)     Upsert by ticket_id + aggregate_version

Worst case: campaign fan-out 10.000 recipients
  - campaign-svc nhận event → check rules → tạo 10.000 dispatch jobs (Hangfire)
  - Batch throttle: 50 emails/s per tenant → 10.000 / 50 = ~200s (3.3 phút)
  - SparkPost/SendGrid burst limit: cần exponential throttle
  - Crash sau 5.000/10.000 jobs: replay → UNIQUE(campaign_id, ticket_id) prevent duplicate

Per-campaign advisory lock (tránh double-dispatch):
  SET campaign:lock:{campaignId}:{ticketId} {instanceId}:{token} NX PX 600000
```

### 8.5 Reliability Rules

- Mọi publish event phải đi qua Outbox Pattern (cùng DB transaction)
- Mọi consumer check `ops.inbox_messages` deduplication (Inbox Pattern)
- Retry: tối đa 5 lần, exponential backoff + jitter → Dead Letter Queue
- `correlationId` truyền qua toàn bộ chain để trace
- Event payload không được breaking change — dùng versioning `.v1`, `.v2`
- Inbox Pattern chỉ dedupe phía async consumer; **không** thay thế HTTP command idempotency

**HTTP command idempotency (`Idempotency-Key`):**

```
- Scope phase 1: POST /tickets, POST /tickets/{id}/messages
- Gateway chỉ forward raw header; support-service mới là nơi enforce và persist
- Client gửi UUID v4 per logical command
- support-service canonicalize payload rồi hash SHA-256 → request_hash
- Flow thực thi dùng 2 transactions:
    1. Tx A (idempotency claim, rất ngắn):
       INSERT ... ON CONFLICT DO NOTHING
       SELECT row FOR UPDATE
       - Completed + same hash → replay response
       - Processing + lock còn hạn → 409 RequestAlreadyInProgress
       - Processing + lock hết hạn → reclaim bằng UPDATE locked_until = NOW()+30s
    2. Tx B (business transaction):
       create/update aggregate + insert outbox + mark idempotency row Completed
- UNIQUE(tenant_id, scope, idempotency_key) là guard cuối cùng
- Same key + same request_hash + status=Completed
    → return lại response đã lưu (same ticketId / messageId), không execute command lần 2
- Same key + different request_hash
    → 409 IdempotencyKeyReuse (ngăn reuse key cho payload khác)
- Same key + status=Processing + locked_until > NOW()
    → 409 RequestAlreadyInProgress
- Crash giữa chừng:
    row `Processing` hết `locked_until` sau 30s → request retry có quyền reclaim và chạy lại
- Retention: 7 ngày; cleanup job mỗi 6h
```

### 8.6 Event Versioning Rules

```
- Event type dùng suffix: .v1, .v2 (ví dụ: support.ticket.created.v1)
- NON-breaking change (thêm optional field): KHÔNG cần version mới
  → Consumer cũ bỏ qua field mới — backward compatible
- BREAKING change (rename/remove field, thay đổi type): BẮT BUỘC v2
  → Publish song song v1 + v2 trong 1 sprint migration window
  → Sau khi toàn bộ consumer migrate sang v2 → ngừng publish v1
- Event payload là append-only: không xóa field của v1 khi publish v2 cùng lúc
- Contract (JSON Schema) lưu tại libs/contracts/events/ — versioned file
```

### 8.7 correlationId / causationId Usage

```
correlationId:
  - ID của user HTTP request ban đầu (hoặc job trigger ban đầu)
  - Propagate qua toàn bộ async chain: request → event → consumer → sub-event
  - Gắn vào mọi log line: logger.info("...", { correlationId, tenantId })
  - Gateway inject: X-Correlation-Id header → downstream services forward
  - OpenTelemetry vẫn tạo `traceId` / `spanId` riêng; `correlationId` được gắn vào log field, baggage, và span attribute để correlate request/business flow xuyên services

causationId:
  - eventId của event trực tiếp gây ra event này
  - support.ticket.created.v1 → causationId = null (triggered by user HTTP)
  - ai.classification.completed.v1 → causationId = support.ticket.created.v1 eventId
  - notification sent → causationId = ticket.created.v1 eventId
  - Dùng để reconstruct causation chain khi debug incident

Ví dụ trace chain:
  HTTP POST /tickets (correlationId: abc-123)
    → support.ticket.created.v1 (correlationId: abc-123, causationId: null)
      → ai.classification.completed.v1 (correlationId: abc-123, causationId: ticket.created eventId)
      → search indexed (correlationId: abc-123 trong log)
      → email sent (correlationId: abc-123 trong log)
  → Tất cả events trong 1 correlationId có thể correlate với cùng OTel trace trong Jaeger/Tempo và cùng log stream trong Loki
```

---

## 9. Business Logic & Luồng Nghiệp Vụ

### Luồng 1: Customer Tạo Ticket Qua Chat Widget

```
Customer mở chat widget
    │
    ▼
ai-service: embed query → kNN hybrid search KB articles
    │
    ├─ confidence ≥ 0.75 → AI trả lời + citation → Customer
    │
    └─ confidence < 0.75 → Escalate
           │
           ▼
    support-service:
      Tx A — Idempotency claim (short transaction)
        INSERT idempotency row nếu chưa có
        SELECT row FOR UPDATE
        - Completed + same request_hash → replay stored 201 response
        - Processing + lock còn hạn → 409 RequestAlreadyInProgress
        - Processing + lock hết hạn → reclaim lock 30s
      Tx B — Business transaction
        allocate ticket_no từ ticket_counters FOR UPDATE
        INSERT INTO support.tickets (...)
        INSERT INTO ops.outbox_events (...)     ← CÙNG transaction
        UPDATE ops.idempotency_keys
          SET response_status=201, response_body={ ticketId, ticketNo },
              resource_type='ticket', resource_id={ticketId},
              status='Completed', completed_at=NOW()
           │
           ▼
    OutboxPublisher (background 500ms) → RabbitMQ
           │
    ┌──────┴────────────────────────┐
    ▼                               ▼                     ▼
notification-svc             ai-svc (classify)       search-svc
Email alert agent            category+priority+       ES index ticket
                             sentiment+tags
                                    │
                                    ▼
                             support-svc consumer
                             UPDATE ticket SET category, priority
                             (check inbox dedup first)
                                    │
                                    ▼
                             Agent Dashboard real-time (WebSocket)
```

### Luồng 2: Agent Xử Lý Ticket

```
Agent mở ticket detail
    │
    ├── GET /api/tickets/{id}
    │     → support-service đọc PostgreSQL trực tiếp
    │     → đây là read-your-writes path, KHÔNG đọc từ Elasticsearch
    │
    ├── [Optional] Agent click "Suggest reply"
    │     → POST /api/ai/tickets/{id}/suggest-reply
    │     → ai-service:
    │          1. GET full ticket thread từ support-service
    │          2. Search similar resolved tickets từ Elasticsearch
    │             (chỉ `body_public_plain`; internal notes không vào retrieval corpus)
    │          3. Load customer_memory từ MongoDB
    │          4. Generate draft reply
    │          5. Cache Redis key `ai:suggest:{ticketId}:{contentHash}` TTL 300s
    │     → FE hiển thị draft để agent sửa trước khi gửi
    │
    ├── Agent gửi reply
    │     → POST /api/tickets/{id}/messages
    │       headers: { Idempotency-Key }
    │       body: { expectedVersion, body_html, body_plain }
    │     → support-service:
    │          1. Tx A — claim `ops.idempotency_keys(scope='ticket.add-message')`
    │             - same key + same request_hash + Completed → trả message đã tạo trước đó
    │             - same key + different request_hash → 409
    │             - Processing + lock còn hạn → 409 RequestAlreadyInProgress
    │          2. Tx B — compare-and-swap bằng `row_version`
    │          3. INSERT support.ticket_messages
    │          4. nếu là first external agent reply → set `first_response_at`
    │          5. INSERT ops.outbox_events `support.ticket.message-added.v1`
    │             payload phải có `authorType`, `isInternalNote`, `customerEmail`
    │          6. UPDATE `ops.idempotency_keys`
    │             SET response_status=201, resource_type='ticket_message',
    │                 resource_id={messageId}, status='Completed', completed_at=NOW()
    │          7. evict `cache:ticket:{tenantId}:{ticketId}`
    │     → notification-service gửi email nếu đây là public agent reply
    │     → search-service chỉ append vào `body_public_plain` nếu không phải internal note
    │
    └── Agent resolve ticket
          → POST /api/tickets/{id}/resolve { expectedVersion }
          → support-service:
               1. compare-and-swap bằng `row_version`
               2. set `status=Resolved`, `resolved_at`, `sla_met`
               3. INSERT support.ticket_status_history
               4. INSERT ops.outbox_events `support.ticket.resolved.v1`
          → fan-out async:
               - notification-service: gửi confirmation / CSAT
               - ai-service: summarize ticket + update customer_memory
               - campaign-service: evaluate follow-up rules
               - search-service: update status/resolved_at trong ES
```

### Luồng 3: Outbox Pattern Chi Tiết

```sql
-- PUBLISH SIDE (application code, cùng 1 DB transaction)
BEGIN;
  INSERT INTO support.tickets (...);
  INSERT INTO ops.outbox_events (
    service_name, aggregate_type, aggregate_id,
    event_type, payload, headers
  ) VALUES ('support-service', 'ticket', '{ticketId}',
    'support.ticket.created.v1', '{...}', '{"correlationId":"...", "tenantId":"..."}');
COMMIT;

-- PUBLISHER SIDE (IHostedService, poll mỗi 500ms)
-- Claim batch an toàn, nhiều instance cùng chạy không conflict
-- [Bug fix v6.0] BẮT BUỘC filter service_name: tất cả services dùng chung ops.outbox_events.
-- Thiếu filter này → support-service publisher có thể claim events của identity-service
-- → publish lên sai exchange/routingKey → silent message loss hoặc routing error.
UPDATE ops.outbox_events
SET status = 'processing',
    claimed_by = 'support-service:instance-1',
    claimed_at = NOW()
WHERE id IN (
  SELECT id FROM ops.outbox_events
  WHERE status = 'pending'
    AND next_retry_at <= NOW()
    AND service_name = 'support-service'   -- [Bug fix v6.0] scope per service
  ORDER BY occurred_at ASC
  FOR UPDATE SKIP LOCKED   ← multi-instance safe
  LIMIT 100
) RETURNING *;

FOR EACH event:
  TRY:
    await rabbitMQ.PublishAsync(exchange, routingKey, event);  -- confirm bắt buộc
    UPDATE SET status='published', published_at=NOW(), claimed_by=NULL
  CATCH:
    UPDATE SET
      status='pending',
      retry_count = retry_count + 1,
      next_retry_at = NOW() + exponential_backoff_jitter(retry_count),
                              -- 5s → 15s → 45s → 2m → 5m
      last_error = error_message,
      claimed_by = NULL
    IF retry_count >= 5:
      UPDATE SET status='dead_letter'   ← alert + manual replay runbook

-- RECLAIM STALE: instance crash giữa chừng
UPDATE ops.outbox_events
SET status='pending', claimed_by=NULL, claimed_at=NULL
WHERE status='processing' AND claimed_at < NOW() - INTERVAL '2 minutes';

-- CONSUMER SIDE (Inbox Pattern — chỉ cho ASP.NET services có PostgreSQL)
-- notification-service, search-service, ai-service dùng Redis/MongoDB inbox (xem Section 4)
BEGIN;
  INSERT INTO ops.inbox_messages (consumer_name, event_id, status='Processing')
  ON CONFLICT (consumer_name, event_id) DO NOTHING;  ← dedup
  -- nếu 0 rows inserted → đã xử lý rồi → skip
  -- process event...
  UPDATE ops.inbox_messages SET status='Done', processed_at=NOW()
  WHERE consumer_name=:name AND event_id=:id;
COMMIT;

-- [Bug fix v6.0] INBOX STUCK-PROCESSING CLEANUP
-- Vấn đề: consumer crash sau INSERT nhưng trước UPDATE status='Done'
-- → row mãi ở 'Processing' → lần sau ON CONFLICT DO NOTHING → event bị bỏ qua vĩnh viễn
-- Giải pháp: cleanup job (Hangfire cron mỗi 5 phút) xóa rows bị stuck
DELETE FROM ops.inbox_messages
WHERE status = 'Processing'
  AND created_at < NOW() - INTERVAL '10 minutes';
-- Sau cleanup, consumer retry từ RabbitMQ sẽ INSERT lại bình thường và xử lý tiếp
```

### Luồng 4: Collision Detection (2 Agents Cùng Reply)

```
Agent A mở ticket →
  SET chat:lock:ticket:{ticketId} "{agentA_id}:{lockToken}" NX PX 30000

Agent B mở cùng ticket →
  GET lock → thấy agentA_id
  → FE: "Agent A is currently replying..." → disable reply editor

Agent A heartbeat mỗi 15s:
  → Lua compare-and-renew:
    if redis.call('get', lockKey) == lockToken then
      redis.call('pexpire', lockKey, 30000)
      return 1
    else return 0 end
  → Chỉ renew TTL nếu token match → owner cũ không renew lock mới

Agent A submit reply:
  1. UPDATE support.tickets
     SET row_version = row_version + 1, updated_at = NOW()
     WHERE id = {ticketId}
       AND row_version = {expectedVersion}     ← compare-and-swap
  2. IF affected_rows = 0:
       → 409 Conflict + latest ticket snapshot
       → FE refetch timeline, không overwrite mù
  3. Release lock: Lua compare-and-delete
     if redis.call('get', lockKey) == lockToken then
       redis.call('del', lockKey)
       return 1
     else return 0 end
  4. WebSocket broadcast "lock_released" → Agent B có thể reply

Ghi chú:
  Redis lock = UX guard (prevent accidental conflict, fast feedback)
  PostgreSQL row_version = concurrency guard cuối cùng (prevent data corruption)
  AI chỉ điền derived fields: category, sentiment, ai_summary
  AI không overwrite manual fields: status, assigned_agent_id, resolved_at
```

### Luồng 5: Ticket Number Allocation

```sql
-- Trong cùng transaction tạo ticket:
INSERT INTO support.ticket_counters(tenant_id) VALUES (:tenantId)
  ON CONFLICT DO NOTHING;

SELECT last_value FROM support.ticket_counters
  WHERE tenant_id = :tenantId FOR UPDATE;   ← row lock per tenant

UPDATE support.ticket_counters
  SET last_value = last_value + 1,
      updated_at = NOW()
  WHERE tenant_id = :tenantId
  RETURNING last_value;

-- format: TK-000123 (zero-padded to 6 digits)
```

Không dùng `SELECT MAX(ticket_no) + 1`: race condition + full scan.
Row lock chỉ nằm trong 1 tenant → tenant A không block tenant B.

**Upgrade path nếu hot tenant > 100 writes/s:** Range allocation — mỗi instance lease block 1000 số.

### Luồng 6: KB Publish → Search Index → AI Corpus

```
Editor nhấn "Publish Article"
    │
    ▼
knowledge-service:
  BEGIN TRANSACTION
    UPDATE knowledge.articles SET status='Published', published_at=NOW()
    INSERT INTO knowledge.article_versions (version = latest + 1, body_markdown, body_html, body_plain)
    INSERT INTO ops.outbox_events (knowledge.article.published.v1,
      payload: { articleId, tenantId, title, summary, body_plain, slug, tags })
  COMMIT
    │
    ▼
OutboxPublisher → RabbitMQ exchange: knowledge → routing: article.published
    │
    └──────────────────────────────────────────┐
                                               ▼
                                       search-service consumer
                                         1. Inbox dedup: Redis SET NX
                                         2. ES upsert text fields (atomic Painless script):
                                            [Bug fix v6.0] KHÔNG dùng GET rồi conditional PUT
                                            → race condition giữa 2 concurrent updates.
                                            Dùng Painless script chạy atomically trên ES shard:

POST kb_articles_v1/_update/{articleId}
{
  "script": {
    "source": "
      if (ctx._source.aggregate_version == null
          || ctx._source.aggregate_version < params.version) {
        ctx._source.putAll(params.doc);
        ctx._source.aggregate_version = params.version;
      } else {
        ctx.op = 'none';  // stale event → no-op, không overwrite
      }
    ",
    "params": {
      "version": <event.aggregateVersion>,
      "doc": { "title": "...", "status": "Published", "tenant_id": "..." }
    }
  },
  "upsert": { ...full doc kèm aggregate_version... }
}

                                         3. Schedule `search.embed-article.v1`
                                            vào queue `search-embed`
                                         4. Search worker gọi OpenAI embeddings
                                            rồi upsert `dense_vector`
                                         5. Mark inbox Done (Redis SET NX đã done ở step 1)

ai-service không consume `knowledge.article.published.v1`.
ai-service chỉ đọc `kb_articles_v1` sau khi projection của search-service hoàn tất.

Stale event guard: [Bug fix v6.0] implement bằng Painless script (atomic trên shard).
Không dùng GET → check → PUT vì race condition giữa 2 concurrent events.
Script so sánh `aggregate_version` và chỉ apply nếu event version > version hiện tại trong ES.
```

---

### Luồng 7: AI Auto-Classify Pipeline

```
support.ticket.created.v1 published → RabbitMQ ai-tasks queue
    │
    ▼
ai-service consumer:
  1. Inbox dedup: (consumer_name='ai-classifier', event_id) → skip nếu đã xử lý
  2. Per-tenant rate limit check:
       Redis INCR ai:ratelimit:{tenantId} (sliding 60s)
       > 20/min → nack(requeue:true) + sleep(backoff) → retry sau
  3. Fetch ticket: HTTP GET support-service /internal/tickets/{ticketId}
  4. Build classify prompt:
       system: "Classify this support ticket. Return JSON: {category, priority, sentiment, tags[]}"
       user: subject + first message body_plain
  5. openai.chat.completions.create (GPT-4o-mini, temperature: 0.1)
       → { category: "billing", priority: "High", sentiment: "Frustrated", tags: ["payment"] }
  6. Log → MongoDB ai_runs:
       { type: "classify", ticketId, tenantId, model, prompt_tokens,
         completion_tokens, latency_ms, result, correlationId }
  7. Publish ai.classification.completed.v1:
       { ticketId, tenantId, category, priority, sentiment, tags,
         correlationId, causationId: ticket.created.eventId }
    │
    ▼
support-service consumer (ai.classification.completed.v1):
  1. Inbox dedup
  2. Fetch current ticket state
  3. Safety check: ticket status NOT IN (Resolved, Closed)?
     AND field chưa được manual override?
  4. UPDATE support.tickets
       SET category = :category,
           priority = :priority,     -- chỉ nếu priority vẫn là default 'Medium'
           sentiment = :sentiment,
           tags = :tags,
           row_version = row_version + 1
       WHERE id = :ticketId
         AND tenant_id = :tenantId
  5. WebSocket broadcast → agent dashboard: "ticket.enriched"
  6. Invalidate Redis cache: cache:ticket:{tenantId}:{ticketId}

Circuit breaker (Polly trên OpenAI HTTP client):
  5 failures trong 30s → OPEN
  OPEN state → classify skipped, ticket tạo không có AI enrichment
  (ticket vẫn tạo thành công — AI là async side effect, không block)
```

---

### Luồng 8: Campaign Follow-Up Sau Resolve

```
support.ticket.resolved.v1 published → RabbitMQ
    │
    ▼ (fan-out — 1 trong 4 consumers)
campaign-service consumer:
  1. Inbox dedup
  2. Fetch active campaigns với trigger = 'ticket_resolved' cho tenantId
  3. Evaluate segment rules:
       customer.tags INTERSECT campaign.segment_tags?
       ticket.category IN campaign.target_categories?
       customer không trong exclusion list?
  4. Match? → Create follow-up schedule:
       Hangfire job: delay = campaign.delay_minutes (ví dụ: 1440 phút sau resolved_at)
       Job payload: { campaignId, ticketId, customerId, tenantId, recipientEmail }
  5. [Bug fix v6.0] Advisory lock — 2 guards khác nhau, không nhầm lẫn:

     Guard A — Advisory lock Redis (10 phút): BẢO VỆ EVENT CONSUMER
       SET campaign:lock:{campaignId}:{ticketId} {instanceId}:{token} NX PX 600000
       → Chỉ cần đủ lâu để schedule xong Hangfire job (vài ms)
       → Tránh 2 instances horizontal scale cùng schedule duplicate job
       → Lock expire sau 10 phút KHÔNG phải vấn đề — job đã được schedule xong rồi

     Guard B — UNIQUE constraint DB: BẢO VỆ ACTUAL DISPATCH (24h sau)
       INSERT (campaign_id, ticket_id, ...) ON CONFLICT DO NOTHING
       → Đây là idempotency guard THỰC SỰ khi Hangfire job chạy 24h sau
       → Lock Redis đã expire từ lâu tại thời điểm này — không liên quan
       → 0 rows inserted = đã dispatch → skip hoàn toàn safe

  Hangfire job execute (24h sau):
  6. Check: ticket vẫn Resolved? (không bị reopen?)
  7. Insert campaign.campaign_dispatches:
       INSERT (campaign_id, ticket_id, recipient_id=customerId, status='Scheduled')
       ON CONFLICT (campaign_id, ticket_id) DO NOTHING  ← idempotent per resolved ticket
       IF 0 rows inserted → đã dispatch rồi, skip
  8. Throttle: Redis counter dispatch:{tenantId}:{minute}
       > 50/min → sleep(1s) + retry
  9. Publish campaign.dispatch.requested.v1
       { campaignId, recipientEmail, templateId, variables: {...}, tenantId }
    │
    ▼
notification-service consumer:
  10. Render Handlebars template với variables
  11. Send email via SparkPost/SMTP
  12. UPDATE campaign_dispatches SET status='Sent', sent_at=NOW()

Crash recovery scenario:
  Hangfire job crash sau step 7 (insert done) nhưng trước step 9:
  → Job restart: INSERT ON CONFLICT DO NOTHING → 0 rows → skip duplicate
  Nếu job crash trước step 7:
  → Job restart: check lại từ đầu → INSERT OK → continue
```

---

## 10. Patterns & Implementation Strategy

### 10.1 Clean Architecture (.NET Services)

```
Dependency rule: Infrastructure → Application → Domain

Domain/         - Entities: pure C# classes, không EF attributes
                - Value Objects: immutable (TicketStatus, Priority)
                - Domain Events: raised trong entity methods
                - Repository Interfaces (chỉ interface, không impl)

Application/    - Commands/Queries: IRequest<T> (MediatR)
                - Handlers: orchestrate domain + infrastructure
                - DTOs: output shapes
                - Validation: FluentValidation pipeline behavior
                - Không có infrastructure code ở đây

Infrastructure/ - EF Core DbContext + Migrations
                - Repository implementations
                - RabbitMQ producer (qua OutboxPublisher)
                - External HTTP clients (HttpClientFactory + Polly)

API/            - Controllers → mediator.Send() → handler
                - No business logic ở controllers
                - Middleware: tenant extraction
```

### 10.2 CQRS (Chỉ Dùng Chọn Lọc)

```
Write side:
  CreateTicketCommand → CreateTicketHandler:
    1. Validate (FluentValidation pipeline)
    2. Create Ticket domain entity
    3. Allocate ticket_no (ticket_counters FOR UPDATE)
    4. ticketRepo.Add(ticket) + outboxRepo.Add(event)
    5. unitOfWork.SaveChanges()  ← single transaction
    6. Return TicketDto

Read side:
  GetTicketListQuery   → Elasticsearch (fast, filter/search)
  GetTicketDetailQuery → PostgreSQL (authoritative, join messages + SLA)
  GetTicketStatsQuery  → PostgreSQL aggregation hoặc analytics cache
```

Dùng CQRS cho: ticket commands/queries, dashboard read model.
Không cần CQRS cho: CRUD đơn giản (feature flags, team management).

### 10.3 Caching Strategy

**Cache-aside pattern:**
- ticket detail hot: `cache:ticket:{tenantId}:{ticketId}` TTL 300s
- KB article: `cache:kb:{tenantId}:{slug}` TTL 3600s
- user permissions: `user_permissions:{userId}:{tenantId}` TTL 3600s
- dashboard widgets: TTL 60–120s

**Invalidation:**
- ticket updated → evict ticket detail cache
- article published → evict KB cache + trigger ISR
- membership changed → evict permission cache

**Cache Stampede Prevention (Thundering Herd):**

Vấn đề: ticket hot bị evict → 200+ concurrent requests hit cache miss → spike vào PostgreSQL.

Giải pháp: **Mutex + Double-Check pattern**

```
1. Request đến → GET cache
2. Cache HIT → return immediately

3. Cache MISS →
   a. SET lockKey {token} NX PX 10000    ← acquire rebuild lock
   b. IF lock acquired (NX succeeded):
        - Double-check: GET cache again   ← tránh race giữa acquire
        - If hit (someone else rebuilt): return
        - Query PostgreSQL → set cache TTL 300s
        - Release lock (Lua compare-and-delete)
   c. IF lock NOT acquired:
        - sleep(50ms)
        - GET cache (bởi thời này đã được rebuild)
        - Return result (may be null if truly not found)
```

> "TTL 10s cho rebuild lock: đủ dài để query + serialize + set cache, đủ ngắn để không block nếu instance crash giữa rebuild. Double-check sau acquire lock tránh race giữa 2 requests cùng acquire cùng lúc."

### 10.4 Concurrency & Consistency Contract

**Source of truth:**
- PostgreSQL write model = authoritative (ticket, assignment, status, SLA)
- Elasticsearch / Redis / MongoDB = projections/cache, có thể trễ vài giây
- Redis lock = UX guard; PostgreSQL row_version = data guard cuối cùng

**Optimistic concurrency (compare-and-swap):**
```sql
UPDATE support.tickets
SET row_version = row_version + 1,
    status = :newStatus,
    updated_at = NOW()
WHERE id = :ticketId
  AND row_version = :expectedVersion;
-- affected_rows = 0 → 409 Conflict → UI refetch + merge
```

**Command idempotency (duplicate HTTP retry protection):**
- `Idempotency-Key` bảo vệ create ticket / add message khỏi duplicate do network retry, browser retry, mobile reconnect
- `expectedVersion` giải quyết stale write giữa 2 actors; `Idempotency-Key` giải quyết cùng 1 actor gửi lại đúng cùng command
- `ops.idempotency_keys` lưu `request_hash`, `response_body`, `locked_until`
- Same key + same hash → replay stored response; same key + different hash → 409

**Field ownership:**
- Agent/manual: `status`, `assigned_agent_id`, `resolved_at`, internal notes
- AI derived only: `category`, `sentiment`, `tags`, `ai_summary`
- AI không overwrite nếu field đã được user sửa hoặc ticket đã Resolved/Closed

**Eventual consistency — khi nào chấp nhận được:**
- Search, analytics, notification là side effects, không block ticket create/resolve
- Ticket detail LUÔN đọc PostgreSQL (read-your-writes)
- Ticket list/search: stale ≤ 5s, tracked bằng metric `occurred_at → indexed_at`

**Ordering:**
- Chỉ cần per-aggregate ordering, không cần global ordering
- ES document chứa `aggregate_version` để tránh older event overwrite newer projection

### 10.5 Failure Modes & Recovery

**Phân loại lỗi:**
- Transient: broker unavailable, SMTP timeout, ES timeout, OpenAI 429/5xx
- Permanent: payload/schema invalid, aggregate không tồn tại, permission mismatch
- Poison message: parse được nhưng fail lặp lại vì data xấu hoặc bug logic

**Retry policy:** exponential backoff + jitter `5s → 15s → 45s → 2m → 5m`
Permanent error → `dead_letter` sớm. Max retry → `dead_letter` + alert.

**Recovery runbook:**
1. Trace theo `correlationId` trong Jaeger/Loki
2. Xác định transient hay permanent
3. Fix root cause (config, code, data)
4. Replay `dead_letter` → `pending` bằng internal tool
5. Inbox Pattern + dedupe keys đảm bảo replay safe

**Timeouts & circuit breakers:**
- AI call timeout: 8–10s → fallback human handoff
- HTTP clients (SMTP, AI): Polly timeout + circuit breaker + retry jitter
- Notification fail không rollback ticket creation (eventual consistency)

### 10.6 Backpressure Mechanism

**Vấn đề:** OpenAI rate limit 429 → ai-service consumer bị block → `ai-tasks` queue phình → broker memory tăng.

```
Layer 1 — Consumer prefetch limit:
  channel.basicQos(prefetchCount: 5)
  → consumer chỉ pull 5 messages cùng lúc (unacked cap)
  → backlog tích lũy ở broker không ở consumer process
  → broker không bị overwhelm bởi unacked messages

Layer 2 — Per-tenant rate limiter:
  Redis sliding window counter: ai:ratelimit:{tenantId}
  IF counter > 20 (jobs/min/tenant):
    nack(requeue: true) + sleep(backoffMs)
  → hot tenant không kéo sập toàn bộ queue

Layer 3 — Circuit breaker (Polly) trên OpenAI HTTP client:
  5 failures trong 30s → circuit OPEN
  OPEN → fallback: Ollama hoặc human handoff ngay
  Không để consumer spin-fail lặp lại

Layer 4 — Monitoring + alert:
  Grafana alert khi:
    - queue depth > 500 steady state > 5 phút
    - oldest ready message age > 30s (bình thường < 5s)
  → manual: scale worker hoặc throttle producer
```

> **Phỏng vấn:** "RabbitMQ không có built-in backpressure như Kafka. Tôi giải quyết bằng `prefetchCount` kiểm soát in-flight messages, per-tenant Redis rate limiter để isolate hot tenant, và Polly circuit breaker để degrade gracefully — thay vì để consumer spin-fail tiêu tốn CPU và block queue."

### 10.7 Performance & Scale Envelope

**Load assumptions:**
- 2–5 tenants active cùng lúc, 50–100 concurrent agents
- 2k–5k tickets/ngày, burst 10–20 ticket writes/phút
- real-time chat burst: 20–30 msg/s trên hot tenant
- AI workload: 2–5 RPS tới LLM (bottleneck #1)

**Bottleneck thứ tự (đo trước, scale sau):**
1. LLM latency/cost
2. Elasticsearch hybrid kNN search
3. Queue backlog khi hot tenant burst
4. N+1 query nếu không kiểm soát projection + eager loading

**Scale levers:**
- Tách queue theo workload: `notifications`, `search-index`, `ai-tasks`, `campaigns`
- Scale ngang: notification, search, ai service độc lập
- Cache: ticket detail, KB, permissions (hit rate target > 75%)
- Batch embedding jobs — không embed đồng bộ trong request path
- Rate limit per tenant + per route
- PgBouncer transaction pooling kiểm soát DB connections

### 10.8 Service SLOs & Error Budgets

| Service | SLO | p95 target | Error budget/30d |
|---------|-----|-----------|-----------------|
| `gateway-bff` | 99.9% | < 150ms | ~43 phút |
| `support-service` | 99.9% | < 200ms write | ~43 phút |
| `search-service` | 99.5% | < 300ms, freshness < 5s | ~3h 39m |
| `notification-service` | 99.5% | enqueue < 200ms, email < 60s | ~3h 39m |
| `ai-service` | 99.0% | < 3s, fallback nếu down | ~7h 18m |
| `campaign-service` | 99.0% | fire trong 2 phút quanh lịch | ~7h 18m |

**Error budget policy:**
- Burn > 25% trong 7 ngày → reliability work ưu tiên hơn feature mới
- `ai-service` burn do provider ngoài → degrade sang human handoff
- Chỉ migrate sang Kafka/K8s khi có hotspot đo được, không pre-scale bằng niềm tin

### 10.9 Observability Bắt Buộc

Mọi service phải emit:
- `correlation_id` + `tenant_id` trong mọi log line
- request duration histogram (p50, p95, p99)
- queue processing duration + DLQ counter
- idempotency replay rate + idempotency conflict rate
- AI latency, search latency (riêng từng loại)
- projection freshness (`occurred_at → indexed_at`)
- version conflict counter (409 Conflict)
- cache hit/miss rate
- backpressure trigger count per tenant
- PgBouncer pool wait time
- circuit breaker state (OPEN/CLOSED/HALF-OPEN)

---

## 11. Trade-offs & Production Reasoning

> Section này trả lời thẳng các câu hỏi "tại sao" thường bị hỏi trong phỏng vấn system design.

---

### 11.1 Tại Sao RabbitMQ Trước Kafka?

```
RabbitMQ phù hợp hơn cho stage hiện tại vì:
  ✓ Workflow-based routing: exchange + binding linh hoạt per consumer
  ✓ DLQ built-in + quản lý dễ qua UI (15672)
  ✓ Vận hành solo đơn giản hơn đáng kể (Kafka cần ZooKeeper/KRaft + partition planning)
  ✓ Message TTL, priority queues native
  ✓ Scale hiện tại: 5k tickets/ngày << RabbitMQ capacity (vài triệu msg/ngày)

Kafka phù hợp hơn khi:
  ✗ Cần ordered events per partition (ví dụ: analytics stream per tenant)
  ✗ Consumer group lag metrics quan trọng (backlog monitoring production)
  ✗ Log compaction (retain last state per key)
  ✗ Throughput > vài trăm nghìn events/ngày

Migration path đã pre-planned:
  Tuần 11–12: thêm Kafka cho analytics stream nếu RabbitMQ throughput đo được là bottleneck
  → Không pre-scale bằng niềm tin, scale bằng số đo thật
```

### 11.2 Tại Sao PostgreSQL Multi-Schema Thay Vì Multi-DB?

```
Multi-DB (mỗi service 1 database instance):
  Pros: isolation tuyệt đối, scale DB độc lập per service
  Cons: N PgBouncer configs, N migration pipelines, không thể cross-schema query,
        overhead vận hành solo tăng N lần

Multi-schema (1 instance, N schemas):
  Pros:
    ✓ 1 PgBouncer config, 1 backup job, 1 monitoring setup
    ✓ Logical isolation đủ tốt khi enforce tenant_id + service boundaries ở application layer
    ✓ ops schema (outbox, inbox, audit) chia sẻ dễ dàng
    ✓ Migration đơn giản: 1 migration runner per service, chỉ access schema của mình
  Cons:
    ✗ 1 noisy neighbor schema có thể tạo lock contention → mitigate: PgBouncer pool limit per service
    ✗ Cannot scale DB per service → upgrade path: tách schema → new instance khi cần

Decision: multi-schema hợp lý cho solo developer + demo portfolio scale.
Upgrade path rõ ràng: không bị lock-in.
```

### 11.3 Tại Sao Eventual Consistency Acceptable Ở Search / Notification / Analytics?

```
Search (Elasticsearch):
  - Ticket list/search = "browse" behavior — user chấp nhận ≤5s lag
  - Ticket detail = critical → luôn đọc PostgreSQL (read-your-writes)
  - Worst case: user tạo ticket → không thấy trong search list 5s → F5 → có ngay
  - Freshness bound rõ ràng: ≤5s, tracked bằng metric (occurred_at → indexed_at)

Notification:
  - Email gửi sau 30–60s là hoàn toàn acceptable trong support workflow
  - Không gửi được → DLQ → retry → alert → manual resolve
  - Notification failure KHÔNG rollback ticket creation (side effect)

Analytics:
  - Dashboard refresh mỗi 60s → stale 5s không ai nhận ra
  - Không dùng cho financial reconciliation → eventual consistent OK

Ranh giới KHÔNG chấp nhận eventual consistency:
  - Ticket state (status, assignment) → PostgreSQL, đồng bộ
  - Auth / RBAC → PostgreSQL + Redis cache (invalidate-on-change)
  - Ticket number allocation → sequential, FOR UPDATE
  - Payment / billing (nếu có) → strong consistency bắt buộc
```

### 11.4 Hot Row / Hot Tenant / Hot Partition

```
Hot row — support.ticket_counters:
  - 1 row per tenant, FOR UPDATE per ticket creation → serialize writes per tenant
  - Acceptable: burst 10–20 writes/phút per tenant (lock held < 1ms)
  - Upgrade nếu > 100 writes/s: range allocation
    → Mỗi instance lease block 1000 số (Redis INCRBY 1000)
    → Không cần DB lock, instance tự quản lý range

Hot tenant — Elasticsearch single index:
  - 1 tenant có corpus lớn → nhiều segments → slower search cho tất cả tenants
  - Mitigate tier 1: per-tenant rate limit trên search endpoint (gateway)
  - Mitigate tier 2: per-tenant index khi corpus > 100k docs
  - Switch dùng index alias: không downtime

Hot tenant — RabbitMQ queue:
  - 1 tenant burst tạo 1000 tickets/phút → flood ai-tasks queue
  - Mitigate: per-tenant Redis rate limiter trong ai-service consumer (nack + requeue nếu > 20/min)
  - Hot tenant bị throttle, các tenant khác không bị ảnh hưởng

Hot partition:
  - Không dùng Kafka → không có partition issue hiện tại
  - Nếu migrate Kafka: partition key = tenantId → hot tenant vẫn là bottleneck
    → Solution: partition key = tenantId XOR random suffix (fan-out key)
```

### 11.5 Scale Levers (Theo Thứ Tự Thực Dụng)

```
1. LLM latency/cost (bottleneck #1 dự đoán):
   → Cache AI responses: suggest-reply cached 300s per ticket per content hash
   → Batch classify offline thay vì realtime
   → Fallback Ollama cho dev/non-critical workloads

2. Elasticsearch kNN search:
   → num_candidates tuning (50 → 100 nếu precision cần tăng)
   → ES hardware: dedicated data nodes với SSD
   → Không scale code, scale infrastructure

3. Queue backlog (hot tenant AI jobs):
   → Per-tenant rate limit (đã implement)
   → Scale horizontal ai-service consumers (Docker Compose replicas)
   → tách queue: ai-classify (fast) vs ai-rag (slow) riêng nhau

4. N+1 query trong support-service:
   → EF Core eager loading + Include()
   → GetTicketList: projection query (không load messages)
   → GetTicketDetail: join messages + SLA policy trong 1 query

5. PgBouncer connection exhaustion:
   → Tăng max_pool_size
   → Tách PgBouncer per service nếu 1 service monopolize connections
   → Cuối cùng: read replica cho analytics/search queries

6. Khi nào scale sang K8s:
   → HPA cần thiết thật sự (không đoán)
   → Multi-region requirement
   → Docker Compose không còn đủ quản lý service dependencies
```

### 11.6 Khi Nào Nâng Cấp Kiến Trúc?

```
RabbitMQ → Kafka:
  Trigger: analytics throughput đo được > 500k events/ngày
           HOẶC cần consumer group lag monitoring chính xác
           HOẶC cần log compaction cho state replay

Multi-schema PG → Multi-DB:
  Trigger: 1 service cần scale DB independently
           HOẶC compliance yêu cầu data isolation cứng
           HOẶC migration bottleneck do shared schema

Docker Compose → K8s:
  Trigger: cần horizontal autoscaling thực sự (traffic không đoán được)
           HOẶC zero-downtime deploy bắt buộc
           HOẶC multi-region

Single ES index → Per-tenant index:
  Trigger: hot tenant ảnh hưởng search latency đo được (p95 > 150ms)
           HOẶC corpus per tenant > 100k documents

Quy tắc: Scale khi có benchmark, không scale trước vì "có thể sẽ cần".
```

---

## 12. AI Integration Strategy

### 12.1 AI Use Cases

| Feature | Trigger | Output |
|---------|---------|--------|
| RAG Chatbot | Customer query | Answer + citations |
| Auto-classify | `ticket.created.v1` | category, priority, sentiment |
| Agent Assist | Agent request | Reply draft |
| Semantic Search | User search | Ranked results |
| Ticket Summary | Assigned/resolved | Summary text |
| Memory Update | `ticket.resolved.v1` | Updated customer profile |

### 12.2 RAG Pipeline

```
Step 1 — EMBED QUERY
  openai.embeddings.create({ model: "text-embedding-3-small", input: query })

Step 2 — RETRIEVE (Elasticsearch hybrid)
  {
    "knn": {
      "field": "embedding",
      "query_vector": [...],
      "k": 5,
      "num_candidates": 50,
      "filter": { "term": { "tenant_id": "uuid" } }   // BẮTBUỘC
    },
    "query": {
      "bool": { "must": { "match": { "body_plain": "..." } } }
    }
  }
  → top 3 chunks (semantic + BM25 hybrid)

Step 3 — AUGMENT
  customer_memory = MongoDB.findOne({ tenant_id, customer_email })
  context = { history_summary, retrieved_articles }

Step 4 — GENERATE
  system_prompt: "Answer ONLY based on provided KB. Cite sources."
  temperature: 0.3 (grounded)

Step 5 — EVALUATE
  // [Bug fix v6.0] calculateConfidence định nghĩa rõ — Phase 1: Score-based (không cần thêm LLM call)
  // Approach được chọn: ES _score normalized + keyword overlap
  // Lý do chọn score-based thay vì self-evaluation prompt:
  //   + Không tốn thêm 1 LLM call (~50ms + cost)
  //   + ES _score phản ánh độ relevant của retrieved context với query
  //   - Kém chính xác hơn self-eval trong edge cases → acceptable ở phase 1

  SCORE_THRESHOLD = 0.7   // tunable, benchmark per dataset
  maxScore = max(retrieved_articles.map(a => a._score))
  scoreNormalized = min(maxScore / SCORE_THRESHOLD, 1.0)

  queryTerms = tokenize(query)
  matchedTerms = queryTerms.filter(t => topArticle.body_plain.includes(t))
  keywordOverlap = matchedTerms.length / queryTerms.length

  confidence = (scoreNormalized * 0.7) + (keywordOverlap * 0.3)

  IF confidence >= 0.75 → serve answer + citations
  ELSE → escalate to agent (return { escalate: true, reason: "low_confidence" })

Step 6 — STORE
  MongoDB: chat_session + citations
  Redis: short-term context TTL 30min
  Async: schedule customer_memory update
```

### 12.3 AI Safety Rules

- Không trả lời nếu retrieval yếu
- Luôn filter retrieval theo `tenant_id`
- Mọi inference log vào MongoDB `ai_runs`
- AI không tự commit business actions nhạy cảm
- AI không overwrite manual fields

---

## 13. API Strategy

### 13.1 Gateway Endpoints

```
# Auth
POST /api/auth/login
POST /api/auth/refresh
GET  /api/auth/me

# Workspace
GET  /api/workspace/me
POST /api/workspace/invite
GET  /api/workspace/members

# Tickets
GET  /api/tickets               ← filters + pagination
POST /api/tickets               ← Idempotency-Key header
GET  /api/tickets/{id}
POST /api/tickets/{id}/messages ← Idempotency-Key header
POST /api/tickets/{id}/assign
POST /api/tickets/{id}/resolve
POST /api/tickets/{id}/close
GET  /api/tickets/{id}/history

# Knowledge Base
GET  /api/kb/articles
POST /api/kb/articles
GET  /api/kb/articles/{id}
PUT  /api/kb/articles/{id}
POST /api/kb/articles/{id}/publish
POST /api/kb/articles/{id}/unpublish

# Search
GET  /api/search?q=...&type=tickets|articles|all

# AI
POST /api/ai/ask                ← RAG chatbot (public widget)
POST /api/ai/tickets/{id}/suggest-reply
GET  /api/ai/tickets/{id}/summary

# Campaigns (optional)
GET  /api/campaigns
POST /api/campaigns
POST /api/campaigns/{id}/schedule
```

**Command safety rules:**
- POST /tickets và POST /tickets/{id}/messages yêu cầu `Idempotency-Key` header từ client-facing flows
- `Idempotency-Key` được persist tại `ops.idempotency_keys`; replay cùng payload trả cùng response, reuse khác payload trả `409`
- Các mutation trên ticket đã tồn tại (`messages`, `assign`, `resolve`, `close`) gửi kèm `expectedVersion` → optimistic concurrency

---

## 14. Roadmap BE — 10 Tuần

### Tuần 1: Foundation
```
✓ NX monorepo setup
✓ docker-compose.infra.yml: PostgreSQL + PgBouncer, MongoDB, Redis, RabbitMQ, ES, MinIO, Mailpit, Ollama
✓ RabbitMQ: exchanges + queues + DLQ + bindings
✓ PostgreSQL: tạo schemas + composite indexes
✓ BuildingBlocks .NET, common NestJS
✓ Structured logging + health checks
✓ GitHub Actions CI
```

### Tuần 2: Identity & Workspace
```
✓ identity-service: register, login JWT, refresh token
✓ workspace-service: tenant provisioning, RBAC seed
✓ gateway-bff: JWT middleware, rate limiting (Redis token bucket), correlation ID
✓ notification-service: workspace.tenant.created.v1 → welcome email
```

### Tuần 3: Support Core
```
✓ support-service: Clean Architecture + CQRS + MediatR
✓ Ticket lifecycle: CreateTicket, Assign, AddMessage, Resolve, Close
✓ ticket_no allocation: ticket_counters + FOR UPDATE
✓ Command idempotency: `ops.idempotency_keys` cho create ticket + add message
✓ OutboxPublisher: ticket events → RabbitMQ
✓ SLA deadline calculation
✓ Audit log: mọi status change ghi ops.audit_logs
✓ notification-service consumers: ticket.created, ticket.message-added, ticket.resolved, ticket.sla-breached
```

### Tuần 4: Knowledge Service
```
✓ knowledge-service: article CRUD, versioning (article_versions), publish workflow
✓ File upload: presigned URL → MinIO
✓ Publish: status=Published + ghi outbox
```

### Tuần 5: RabbitMQ + Outbox + Search
```
✓ Outbox Pattern hoàn chỉnh (SKIP LOCKED, backoff + jitter, DLQ, reclaim stale)
✓ Inbox Pattern: ops.inbox_messages deduplication
✓ search-service: ES client, article + ticket indexing consumers
✓ Hybrid search endpoint (BM25 + kNN)
✓ Analytics cơ bản, Grafana seed
```

### Tuần 6: Real-time Chat
```
✓ Socket.io với Redis adapter (multi-instance safe)
✓ Presence tracking: Redis TTL 30s + heartbeat
✓ Collision detection: Redis advisory lock (Lua compare-and-renew)
✓ Chat sessions: MongoDB
```

### Tuần 7: AI v1
```
✓ ai-service: LLM factory (OpenAI + Ollama fallback)
✓ Embedding pipeline: article.published → embed → ES dense_vector
✓ RAG: hybrid kNN, grounded prompt, confidence threshold, citation
✓ Auto-classify: ticket.created → category + priority + sentiment
✓ Agent Assist: suggest-reply endpoint, Redis cache 300s
✓ Customer memory: ticket.resolved → MongoDB update
✓ ai_runs logging
```

### Tuần 8: Caching + Rate Limiting + Performance
```
✓ Redis cache-aside: ticket detail, KB, permissions
✓ Cache stampede: mutex + double-check
✓ Rate limiting: gateway per tenant + per endpoint
✓ Prefetch limit: RabbitMQ consumers basicQos(5)
✓ Per-tenant rate limiter trong ai-service consumer
✓ Circuit breaker Polly trên OpenAI HTTP client
✓ PgBouncer transaction pooling + pool wait monitoring
✓ k6 load test: 50 VU, 3 phút → EXPLAIN ANALYZE top 3 slow queries
```

### Tuần 9: Campaign + Polish
```
✓ campaign-service: CampaignDefinition, Hangfire, throttle 50/s per tenant
✓ Per-campaign advisory lock
✓ Post-resolution follow-up: ticket.resolved → schedule follow-up
✓ Demo data: 2 tenants, 10 agents, 100+ tickets, 30+ KB articles
```

### Tuần 10: Deploy + Observability
```
✓ Docker Compose production: resource limits, health checks, named volumes
✓ Nginx: reverse proxy, SSL (Let's Encrypt), gzip
✓ GitHub Actions: ci.yml + deploy-production.yml
✓ OpenTelemetry: .NET + NestJS → Jaeger/Tempo
✓ Prometheus + Grafana: latency, queue depth, error rate, AI latency, SLO burn rate
✓ Loki + Promtail, Sentry, UptimeRobot
✓ k6 final: 100 VU, 5 phút → p95 < 200ms documented
```

---

## 15. Metrics & Production Dashboards

### 15.1 Metrics Cần Đo

| Metric | Target | Tool |
|--------|--------|------|
| API p95 latency | < 200ms | Prometheus + k6 |
| API p99 latency | < 500ms | Prometheus |
| Throughput | > 200 req/min | k6 |
| Cache hit rate | > 75% | Redis INFO + Grafana |
| RabbitMQ queue depth | < 100 steady state | RabbitMQ + Prometheus |
| Oldest ready message age | < 5s | RabbitMQ + Prometheus |
| ES search latency | < 80ms | ES slow log |
| Projection freshness (PG → ES) | < 5s | Custom metric |
| AI response time | < 3s | ai_runs aggregation |
| AI confidence avg | > 0.80 | MongoDB ai_runs |
| Version conflict rate (409) | track, alert nếu tăng đột biến | API metrics |
| Idempotency replay / conflict rate | track, alert nếu conflict tăng | API metrics |
| Ticket counter lock wait | < 50ms p95 | PG lock metrics |
| DLQ count | 0 (alert nếu > 0) | RabbitMQ |
| PgBouncer pool wait | < 10ms p95 | PgBouncer metrics |
| Backpressure trigger count/tenant | track per tenant | Custom metric |
| Circuit breaker state | alert nếu OPEN > 30s | Polly metrics |
| SLO burn rate | alert > 2× budget trong 1h/6h | Grafana |
| Uptime | > 99.5% 30 ngày | UptimeRobot |

---

### 15.2 Production Dashboards Quan Trọng Nhất (Grafana)

#### Dashboard 1: API Health — "Hệ thống đang chạy tốt không?"

```
Panels:
  - Request rate per service (RPS) — time series
  - p50 / p95 / p99 latency per service — time series (alert p95 > 200ms)
  - Error rate 4xx / 5xx per service — heatmap
  - SLO burn rate: sliding window 1h / 6h / 24h / 3d — gauge
  - HTTP 409 Conflict rate (optimistic lock) — counter (spike = concurrency issue)
  - Active WebSocket connections — gauge

Dùng khi: daily health check, sau deploy, khi user báo chậm
```

#### Dashboard 2: Queue Health — "Event pipeline có bị nghẽn không?"

```
Panels:
  - RabbitMQ queue depth per queue:
      notifications | ai-tasks | search-index | campaigns | DLQ
  - Oldest ready message age per queue (alert > 30s)
  - Consumer throughput (messages/s processed)
  - DLQ message count — gauge (alert ngay nếu > 0)
  - Outbox: pending events count — gauge (alert nếu > 50 quá 5 phút)
  - Backpressure trigger count per tenant — bar chart

Dùng khi: debugging slow notification, AI enrichment chậm, sau RabbitMQ restart
```

#### Dashboard 3: AI Pipeline — "AI đang hoạt động ở chất lượng nào?"

```
Panels:
  - ai-service HTTP RPS tới LLM provider — time series
  - AI response p95 latency (classify / rag / summarize riêng nhau)
  - Circuit breaker state (OPEN / CLOSED / HALF-OPEN) — state panel (alert OPEN > 30s)
  - AI confidence score avg (7-day rolling) — gauge (alert < 0.70)
  - Escalation rate (confidence < 0.75 → agent) — percentage
  - ai_runs count per type per day — bar chart (classify / rag / summarize)
  - Fallback to Ollama rate — counter

Dùng khi: review AI quality weekly, debug "AI không trả lời đúng"
```

#### Dashboard 4: Tenant Health — "Có hot tenant nào không?"

```
Panels:
  - Ticket creates per tenant per hour — stacked bar
  - Per-tenant rate limit triggers (backpressure) — table
  - Per-tenant cache hit rate — bar chart (target > 75%)
  - Per-tenant AI job queue depth — bar chart
  - Per-tenant search latency p95 — table
  - Per-tenant error rate — table (alert nếu 1 tenant >> avg)

Dùng khi: tenant báo chậm, debug isolation issue, capacity planning
```

#### Dashboard 5: Infrastructure — "DB/cache/infra có vấn đề không?"

```
Panels:
  - PgBouncer active connections vs pool_size — gauge (alert > 80%)
  - PgBouncer pool wait time p95 — time series (alert > 10ms)
  - PostgreSQL: active queries, locks waiting, index scan ratio
  - Redis memory usage — gauge (alert > 80% maxmemory)
  - Redis eviction rate — counter (eviction = cache hit rate sẽ drop)
  - Elasticsearch heap usage — gauge (alert > 75%)
  - Elasticsearch indexing rate vs search rate — dual time series
  - MinIO disk usage — gauge

Dùng khi: performance degradation không rõ nguyên nhân, capacity review
```

---

## 16. Portfolio & Interview Prep BE

### 5 WOW Points

**WOW #1: Multi-tenant isolation 3 lớp**
> "Mọi query mandatory filter `tenant_id`. tenantId extract từ JWT tại Gateway, forward qua header — client không inject được tenantId tùy ý. Redis keys, ES queries, AI retrieval — tất cả filtered theo tenant."

**WOW #2: Outbox + Inbox = zero message loss + effectively-once**
> "Ticket creation và event publish trong cùng DB transaction. Nếu RabbitMQ down 30 phút, OutboxPublisher tự replay toàn bộ missed events khi recover. Inbox deduplication ngăn consumer xử lý event 2 lần."

**WOW #3: RAG với confidence-based escalation**
> "Không chỉ gọi ChatGPT random — retrieve KB của đúng tenant qua hybrid search (BM25 + kNN), build grounded context với customer long-term memory, confidence < 0.75 tự escalate sang agent thay vì hallucinate."

**WOW #4: Concurrency production-grade**
> "3 tầng: Redis advisory lock với Lua compare-and-renew (UX guard), PostgreSQL row_version compare-and-swap (data guard), Inbox Pattern (message guard). ticket_counters với FOR UPDATE per tenant cho ticket_no không race condition."

**WOW #5: Backpressure + Circuit Breaker**
> "RabbitMQ prefetchCount kiểm soát in-flight. Per-tenant Redis rate limiter isolates hot tenant. Polly circuit breaker trên OpenAI client degrade sang human handoff — không để queue spin-fail."

### Interview Q&A

**Q: Tại sao dùng RabbitMQ thay Kafka?**
A: RabbitMQ phù hợp workflow-based messaging, DLQ tốt, dễ vận hành solo. Kafka phù hợp ordered stream, log compaction, consumer group lag analytics. Nếu analytics stream thành bottleneck thật sự (đo được), tôi migrate sang Kafka — đó là stretch goal đã pre-planned.

**Q: Outbox Pattern hoạt động thế nào?**
A: Entity và event ghi cùng 1 DB transaction. Publisher claim batch bằng `FOR UPDATE SKIP LOCKED` — safe cho multiple instances. Publish với broker confirm, rồi mới mark `published`. Crash sau commit DB nhưng trước publish → row outbox vẫn còn, replay tự động. Inbox Pattern ở consumer: at-least-once + idempotent = effectively-once.

**Q: Tại sao eventual consistency OK ở đây?**
A: Search, analytics, notification là side effects, không quyết định correctness của ticket. `create/resolve` commit vào PostgreSQL, Elasticsearch đến sau vài giây là chấp nhận được. Ticket detail luôn đọc PostgreSQL (read-your-writes). Freshness bound rõ ràng: ≤ 5s, tracked bằng metric.

**Q: Redis lock của bạn safe thế nào?**
A: Lock value chứa `lock_token` (UUID per owner). Renew bằng Lua compare-and-renew: chỉ extend TTL nếu token match — owner cũ không renew lock mới. Release bằng Lua compare-and-delete. Tất cả atomic.

**Q: Cache stampede bạn xử lý thế nào?**
A: Mutex + double-check. Request đầu tiên acquire Redis lock (NX + 10s TTL), rebuild cache, release. Các request khác wait 50ms rồi read từ cache vừa rebuild. Double-check sau acquire tránh race giữa 2 requests cùng lúc.

**Q: Backpressure khi OpenAI rate limit?**
A: prefetchCount giữ in-flight ở mức kiểm soát. Per-tenant Redis rate limiter: vượt 20 AI jobs/min/tenant → nack + requeue + backoff. Polly circuit breaker: 5 lỗi/30s → OPEN → fallback Ollama hoặc human handoff.

**Q: System nghẽn ở đâu đầu tiên khi scale?**
A: (1) LLM latency/cost, (2) ES hybrid kNN, (3) queue backlog khi hot tenant burst, (4) N+1 query. Giải quyết theo thứ tự này. Kafka chỉ hợp lý khi analytics throughput đo được là bottleneck.

**Q: Composite indexes bạn chọn như thế nào?**
A: Equality filter trước, range/sort sau. `(tenant_id, status)`: tenant_id trước vì mọi query scope tenant. Partial index `WHERE status NOT IN (Resolved, Closed)` giảm index size. Verify bằng `EXPLAIN ANALYZE`, không index blindly.

**Q: Replay DLQ không gây double side effect thế nào?**
A: Consumer phải idempotent. Email dùng `dedupe_key`. Search dùng ES upsert theo document_id + `aggregate_version`. Campaign dispatch có UNIQUE(campaign_id, ticket_id). Inbox Pattern chặn theo `(consumer_name, event_id)`.

**Q: PgBouncer bạn dùng ở mode nào và tại sao?**
A: Transaction pooling mode. 8 services × pool 10 = ~80+ connections. PgBouncer giữ actual PostgreSQL connections xuống 20–30. Session pooling không phù hợp vì EF Core không cần giữ session state qua requests. Theo dõi pool_wait metric và alert nếu > 10ms p95.

### CV Bullet Points BE

```
• Architected a multi-tenant SaaS customer support backend (8 microservices: ASP.NET Core
  + NestJS) with event-driven communication via RabbitMQ using Outbox + Inbox Pattern,
  ensuring zero message loss and idempotent processing; p95 API latency < 200ms at
  200 req/min under k6 load testing.

• Implemented production-grade concurrency controls: PostgreSQL optimistic locking
  (row_version compare-and-swap, 409 Conflict), Redis distributed advisory locks
  (Lua atomic compare-and-renew/delete), cache stampede prevention (mutex + double-check),
  and per-tenant backpressure (prefetch + rate limiter + Polly circuit breaker on AI provider).

• Built a production-grade RAG pipeline (LangChain.js + OpenAI text-embedding-3-small +
  Elasticsearch hybrid search BM25 + kNN) with confidence-based escalation, tenant-isolated
  retrieval, long-term customer memory, and full ai_run logging; handled 60%+ queries
  without agent intervention.

• Delivered full observability (OpenTelemetry distributed tracing, Prometheus/Grafana
  per-service SLOs with error budget policy, Loki log aggregation, PgBouncer connection
  pooling) and CI/CD via GitHub Actions + Docker Compose, maintaining 99.5% uptime
  over 30 days.
```

---

## 17. Cut-Scope Guide

**Cắt theo thứ tự (trên trước):**
1. `campaign-service` (tuần 9)
2. Kafka migration (tuần 11–12)
3. K8s
4. Advanced customer memory
5. Collision detection Redis lock (giữ WebSocket, bỏ advisory lock — degrade gracefully)

**Không được cắt:**
- tenant + RBAC
- ticket lifecycle + Outbox Pattern
- KB + search (keyword tối thiểu)
- AI summary + RAG có citation
- deploy thật (URL live)
- observability cơ bản (1 Grafana dashboard)

**Fallback nếu trễ nhiều:** Merge `identity-service` + `workspace-service` thành `auth-service`. Compromise hợp lý.

---

## 18. Context Handoff BE

### Khi Mang File Này Sang Chat Window Mới

```
Đây là source of truth cho Backend của SignalDesk AI (v5.0-BE).
Hãy dùng nó làm base và giúp tôi: [ghi rõ task].

Context hiện tại:
  Tuần:            [N]
  Đã hoàn thành:   [liệt kê]
  Đang implement:  [feature]
  Service focus:   [service name]
```

### Quyết Định Đã Chốt

```
Message broker:    RabbitMQ (Kafka tuần 11-12 nếu cần)
BE services:       identity + workspace + support + knowledge +
                   notification + search + ai (NX)
DB:                PostgreSQL multi-schema + PgBouncer (transaction pooling)
                   + MongoDB + Redis
Search:            Elasticsearch 8 single index, tenant_id filter bắt buộc
Concurrency:       row_version CAS + Redis Lua lock + Inbox dedup
Cache stampede:    mutex + double-check + lock rebuild (NX + 10s TTL)
Backpressure:      prefetchCount(5) + per-tenant Redis rate limiter + Polly
Deploy:            Docker Compose + Nginx + GitHub Actions
Indexes:           Composite (tenant_id first) + partial index cho hot paths
```

### Trạng Thái Hiện Tại

```
Phiên bản: v5.0-BE
Tuần:      [ ]
Deliverables hoàn thành:
  - [ ]
Đang implement:
  - [ ]
```

---

*Cập nhật `Trạng Thái Hiện Tại` khi bắt đầu mỗi tuần mới.*

---

## 19. Backend Requirement Compliance Matrix

> Section này dùng để đối chiếu trực tiếp với yêu cầu kỹ thuật backend khi bảo vệ đồ án. Nếu hội đồng hỏi "hệ thống có dùng X không?", câu trả lời nằm trong bảng dưới đây.

### 19.1 Nhóm 1 — Giao Tiếp & Hạ Tầng

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| API Gateway | ✅ Có | `gateway-bff` dùng NestJS làm BFF/API Gateway. Gateway xử lý routing, JWT auth middleware, tenant resolution, centralized rate limiting bằng Redis token bucket, correlation ID, WebSocket entrypoint. |
| Request aggregation | ✅ Bổ sung | Gateway có các endpoint aggregation cho dashboard: `GET /api/dashboard/overview`, `GET /api/tickets/{id}/detail-view`. Gateway gọi song song support/workspace/search/ai-service rồi gom response, nhưng không chứa business logic. |
| gRPC internal communication | ✅ Bổ sung | Dùng gRPC cho các internal sync call cần low-latency và strongly typed contract: `workspace.AuthorizationGrpc.CheckPermission`, `identity.UserGrpc.GetUserProfile`, `support.TicketGrpc.GetTicketSnapshot`. REST vẫn dùng cho client-facing API. |
| Message Queue | ✅ Có | RabbitMQ là message broker chính cho event-driven processing: notification, AI enrichment, search indexing, campaign dispatch. Kafka là stretch goal cho analytics stream nếu throughput tăng. |
| WebSocket / SignalR | ✅ Có | Socket.io WebSocket qua gateway cho live chat, typing indicator, presence, lock status, ticket update realtime. Nếu chuyển gateway sang .NET thì có thể thay bằng SignalR. |
| Docker + Docker Compose | ✅ Có | Toàn bộ service và infra được containerize bằng Docker Compose: PostgreSQL, PgBouncer, MongoDB, Redis, RabbitMQ, Elasticsearch, object storage, observability stack. |

**gRPC boundary rule:**
```
Client -> Gateway -> REST/HTTP
Service -> Service internal sync -> gRPC when contract stable and latency-sensitive
Service -> Service async side effect -> RabbitMQ
```

**gRPC proto ownership:**
- `.proto` files đặt tại `libs/contracts/grpc/`
- Mỗi service own proto của public internal API do service đó expose
- Breaking change phải tạo package/version mới, ví dụ `workspace.authz.v2`

---

### 19.2 Nhóm 2 — Dữ Liệu & Tìm Kiếm

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| PostgreSQL | ✅ Có | Dùng cho transactional data: identity, workspace, support, knowledge, campaign, ops. Lý do: ACID, transaction, locking, indexes, outbox/idempotency/audit cần consistency mạnh. |
| MongoDB | ✅ Có | Dùng cho document/semi-structured data: `ai_runs`, `chat_sessions`, `customer_memory`, `conversation_summaries`, `notifications`, `email_templates`. Lý do: schema linh hoạt, lưu log AI/prompt/context dễ thay đổi. |
| Redis L1 + L2 cache | ✅ Bổ sung | L1 in-memory cache trong từng service cho permissions/feature flags/read config TTL 30-60s. L2 Redis distributed cache cho ticket detail, KB, permissions, rate limit, idempotency short lock, presence, distributed lock. |
| Elasticsearch | ✅ Có | Dùng cho full-text search, hybrid search, ticket/article read model, log/behavioral analytics nếu dùng ELK. |
| Vector Database | ✅ Bổ sung | Chọn **Qdrant** làm vector database chính cho RAG/semantic search trong bản production rubric. Elasticsearch vẫn giữ vai trò BM25/full-text. Nếu muốn giảm infra cho demo, có thể dùng PostgreSQL `pgvector` thay Qdrant. |

**Service dùng PostgreSQL và lý do:**
- `identity-service`: user account, refresh token, email verification; cần transaction và unique constraint.
- `workspace-service`: tenant, membership, RBAC, feature flags; cần consistency mạnh khi đổi quyền.
- `support-service`: ticket lifecycle, messages, SLA, audit; là core transactional source of truth.
- `knowledge-service`: article metadata, versioning, publish workflow; cần immutable article versions.
- `campaign-service`: campaign definition, dispatch status; cần idempotency và scheduling state.
- `ops` schema: outbox, inbox, audit, idempotency; cần transaction chung với write model.

**Service dùng MongoDB và lý do:**
- `ai-service`: `ai_runs`, `chat_sessions`, `customer_memory`; prompt/context thay đổi thường xuyên, cần lưu JSON/document linh hoạt.
- `notification-service`: in-app notification history, email templates; document shape khác nhau theo channel/template.

**Vector store decision: Qdrant vs pgvector**
```
Default production choice: Qdrant
  - collection: kb_chunks
  - vector size: 1536 for OpenAI text-embedding-3-small
  - payload filter: tenant_id, article_id, version, status='Published'
  - used by: ai-service retriever, semantic search

Demo fallback: pgvector
  - table: knowledge.article_chunks
  - column: embedding vector(1536)
  - index: HNSW or IVFFlat
  - simpler deployment when avoiding a separate vector DB
```

**Hybrid retrieval flow:**
```
1. Elasticsearch BM25 retrieves keyword candidates
2. Qdrant retrieves semantic candidates with mandatory tenant_id filter
3. ai-service merges and reranks candidates
4. top chunks injected into LLM prompt with citations
```

---

### 19.3 Nhóm 3 — File & Object Storage

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Supabase Storage / Cloudflare R2 | ✅ Bổ sung | Production/demo cloud dùng Cloudflare R2 hoặc Supabase Storage qua S3-compatible API. MinIO chỉ dùng local development để giả lập S3. |
| Không lưu file trên disk server | ✅ Có | File/ảnh/tài liệu upload bằng presigned URL trực tiếp lên object storage. Backend chỉ lưu metadata và object key. |
| Dễ migrate S3 SDK | ✅ Có | `knowledge-service` dùng abstraction `IObjectStorageClient`; provider local = MinIO, provider cloud = Cloudflare R2/Supabase Storage. |

**Object storage policy:**
```
Local dev:        MinIO
Demo cloud:       Cloudflare R2 hoặc Supabase Storage
API style:        S3-compatible presigned URL
Metadata store:   PostgreSQL knowledge.article_attachments
Binary source:    Object storage bucket, never server disk
```

---

### 19.4 Nhóm 4 — Bảo Mật & Identity

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Identity Service | ✅ Có | `identity-service` custom Auth Service bằng ASP.NET Core 8. Có thể thay bằng Keycloak nếu muốn chuẩn enterprise hơn. |
| OAuth2 / OpenID Connect | ✅ Bổ sung | Custom Auth Service expose OIDC-compatible endpoints: discovery, JWKS, authorize/token/logout/userinfo. JWT access token RS256, refresh token rotation. |
| JWT + Refresh Token rotation | ✅ Có | Access token 15 phút, refresh token 30 ngày, hashed token, rotate mỗi lần refresh, revoke khi membership bị remove. |
| RBAC | ✅ Có | Role/permission trong `workspace-service`, cache Redis, invalidate khi membership/role thay đổi. |
| ABAC | ✅ Bổ sung | Policy engine kiểm tra attribute động: tenant plan, membership status, ticket ownership, department/team, feature flag, business hours. |
| Audit Log Service | ✅ Có | `ops.audit_logs` ghi ai, làm gì, lúc nào, aggregate nào, correlation ID. |
| Idempotency Key | ✅ Có | `ops.idempotency_keys` bảo vệ create ticket/add message/campaign dispatch khỏi duplicate khi retry. |

**OIDC endpoints của custom identity-service:**
```
GET  /.well-known/openid-configuration
GET  /.well-known/jwks.json
POST /connect/token
POST /connect/logout
GET  /connect/userinfo
```

**ABAC examples:**
```
CanViewTicket(user, ticket):
  user.tenantId == ticket.tenantId
  AND user.membershipStatus == 'Active'
  AND (
    user.role in ['Admin', 'Manager']
    OR ticket.assignedAgentId == user.id
    OR ticket.teamId in user.teamIds
  )

CanUseAIFeature(user, tenant):
  tenant.plan in ['Pro', 'Enterprise']
  AND featureFlags.ai_copilot == true
  AND user.permissions contains 'ai.use'
```

---

### 19.5 Nhóm 5 — Resilience & Architecture Patterns

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Clean Architecture | ✅ Có | Áp dụng trong từng .NET service: Domain, Application, Infrastructure, Api. NestJS tách module theo domain/infrastructure. |
| CQRS | ✅ Có | Commands cho write pipeline, Queries cho read pipeline. Dùng rõ nhất ở support-service và dashboard/search read model. |
| DDD | ✅ Bổ sung rõ | Bounded Context: Identity, Workspace, Support, Knowledge, Notification, Search, AI, Campaign. Aggregate Root: User, Tenant, Ticket, Article, Campaign. Domain Event phát sinh từ aggregate method. |
| Event Sourcing | ✅ Bổ sung có phạm vi | Áp dụng scoped Event Sourcing cho `support.ticket_events` để lưu lịch sử thay đổi ticket. PostgreSQL `support.tickets` vẫn là current-state projection để query nhanh. |
| Saga Pattern | ✅ Bổ sung | Dùng Saga orchestration cho tenant onboarding và campaign dispatch; choreography cho ticket side effects như notification/search/AI. |
| Outbox Pattern | ✅ Có | Mọi domain event publish qua outbox trong cùng DB transaction. Publisher dùng SKIP LOCKED, broker confirm, retry, DLQ. |
| Circuit Breaker / Retry / Timeout | ✅ Có | Polly cho .NET HTTP/LLM calls; NestJS interceptor hoặc `opossum`/custom policy cho external calls. |

**Bounded Context map:**
```
Identity Context:    User, RefreshToken, EmailVerification
Workspace Context:   Tenant, Membership, Role, Permission, FeatureFlag
Support Context:     Ticket, TicketMessage, Customer, SLA Policy
Knowledge Context:   Article, ArticleVersion, Attachment
Campaign Context:    CampaignDefinition, CampaignDispatch
AI Context:          AIRun, ChatSession, CustomerMemory
Notification Context:Notification, EmailTemplate, DeliveryAttempt
Search Context:      SearchDocument, ProjectionState
```

**Aggregate roots:**
- `Ticket` owns status, assignment, messages, SLA fields, row_version, ticket domain events.
- `Article` owns draft/publish state and immutable `ArticleVersion`.
- `Tenant` owns plan, settings, feature flags, membership policy.
- `CampaignDefinition` owns targeting rule and schedule policy.

**Scoped Event Sourcing for ticket timeline:**
```
support.ticket_events
  id
  tenant_id
  ticket_id
  sequence_no
  event_type
  event_payload
  actor_id
  occurred_at
  correlation_id

Write flow:
  1. Ticket aggregate handles command
  2. Append event to support.ticket_events
  3. Update support.tickets current-state projection
  4. Insert outbox event in same transaction
```

**Why scoped, not full-system Event Sourcing:**
- Ticket timeline benefits from exact audit/history and replay.
- Identity/workspace/KB CRUD do not need full replay complexity.
- This satisfies event history requirement while keeping demo feasible.

**Saga examples:**
```
TenantOnboardingSaga (orchestration)
  1. identity.user.registered.v1
  2. Create tenant
  3. Create admin membership
  4. Seed default roles/permissions
  5. Send welcome email
  Compensation: if membership seed fails, mark tenant ProvisioningFailed and alert.

CampaignDispatchSaga (orchestration)
  1. Select eligible recipients
  2. Create campaign_dispatch rows with idempotency
  3. Publish notification.send-email jobs
  4. Track delivery status
  Compensation: failed delivery moves to DLQ/manual retry, never duplicate-send.

TicketCreated side effects (choreography)
  support.ticket.created.v1 -> notification/search/ai consumers independently process side effects.
```

---

### 19.6 Nhóm 6 — AI & LLM Integration

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| LLM API | ✅ Có | OpenAI GPT-4o-mini primary, Ollama fallback. Có thể cấu hình Anthropic Claude làm provider thứ hai qua `ILLmProvider`. |
| RAG Pipeline | ✅ Có | Chunking -> embedding -> Qdrant/pgvector vector store -> semantic search -> context injection -> LLM response -> citation/confidence. |
| AI Agent | ✅ Bổ sung | `ai-service` có agent mode cho multi-step reasoning và tool calling giới hạn: search KB, fetch ticket snapshot, draft reply, create internal note suggestion. |
| Python AI/ML Microservice | ✅ Optional bổ sung | `analytics-ml-service` Python/FastAPI optional cho forecasting, clustering, heavy analytics; gọi qua gRPC hoặc REST từ ai-service. |

**AI agent tool policy:**
```
Allowed tools:
  - search_knowledge_base(query, tenantId)
  - get_ticket_snapshot(ticketId, tenantId)
  - get_customer_memory(email, tenantId)
  - draft_reply(ticketId, tone)
  - suggest_internal_note(ticketId)

Forbidden direct actions:
  - resolve ticket
  - send email
  - change assignee
  - modify customer data

Rule: AI returns suggestion; human agent approves business action.
```

---

### 19.7 Nhóm 7 — Notification & Integration

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Multi-channel Notification | ✅ Bổ sung đầy đủ | In-app realtime qua WebSocket, email qua SendGrid/Resend/SparkPost/SMTP, push notification qua Web Push/FCM optional. |
| Webhook System | ✅ Bổ sung | `webhook-service` hoặc module trong workspace-service cho third-party event subscription. |
| Feature Flags | ✅ Có | `workspace.feature_flags`, Redis cache TTL 600s, kiểm tra theo tenant/user/group. |

**Webhook design:**
```
workspace.webhook_subscriptions
  id
  tenant_id
  target_url
  secret
  subscribed_events
  status
  created_at

webhook.delivery_attempts
  id
  subscription_id
  event_id
  status
  response_code
  retry_count
  next_retry_at
```

**Webhook delivery rules:**
- Payload ký bằng HMAC SHA-256: `X-SignalDesk-Signature`.
- Retry exponential backoff tối đa 5 lần.
- Failed delivery vào DLQ/manual replay.
- Idempotency key gửi cho bên thứ ba: `event_id`.
- Tenant chỉ nhận events thuộc tenant đó.

---

### 19.8 Nhóm 8 — Background Processing

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Background Job Queue | ✅ Có | Hangfire cho .NET scheduled/background jobs; BullMQ cho NestJS queue jobs như embedding, email, AI summary/suggestion. |
| Retry failed job | ✅ Có | Retry exponential backoff + jitter; permanent failure vào dead letter/manual review. |
| Dead Letter Queue | ✅ Có | RabbitMQ DLQ cho event/job message lỗi; dashboard alert khi DLQ count > 0. |

**Job ownership:**
- Hangfire: refresh token cleanup, SLA breach scan, campaign scheduling, daily retention cleanup.
- BullMQ: email rendering/sending, article embedding, AI summary, AI suggest reply, webhook delivery.

---

### 19.9 Nhóm 9 — Observability & DevOps

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Distributed Tracing | ✅ Có | OpenTelemetry instrument .NET + NestJS, export sang Jaeger/Tempo. |
| Centralized Logging | ✅ Bổ sung option | Default dùng Loki + Promtail. Nếu rubric yêu cầu ELK/Seq: dùng ELK Stack cho log analytics hoặc Seq cho .NET structured logs. |
| Monitoring & Alerting | ✅ Có | Prometheus + Grafana metrics dashboard, alert rules, SLO burn rate. |
| Health Check | ✅ Bổ sung rõ | Mỗi service expose `/health/live` và `/health/ready`. Gateway aggregate health tại `/api/health`. |
| CI/CD Pipeline | ✅ Có | GitHub Actions build -> test -> lint -> docker build -> deploy. GitLab CI là alternative nếu đổi platform. |

**Health endpoints:**
```
GET /health/live
  - process alive
  - no downstream dependency check

GET /health/ready
  - PostgreSQL/MongoDB/Redis/RabbitMQ/Elasticsearch reachable as needed by service
  - migrations compatible
  - queue consumer ready

GET /api/health
  - gateway aggregated readiness summary
```

**Logging stack choices:**
```
Option A (current): Loki + Promtail + Grafana
Option B (rubric-compatible): ELK = Elasticsearch + Logstash + Kibana
Option C (.NET-friendly): Seq + Serilog sink
```

---

### 19.10 Nhóm 10 — Testing & Documentation

| Yêu cầu | Trạng thái | Thiết kế áp dụng trong SignalDesk AI |
|---------|------------|---------------------------------------|
| Unit Test | ✅ Bổ sung | xUnit cho ASP.NET Core services, Jest cho NestJS services. Coverage report publish trong CI. |
| Integration Test | ✅ Bổ sung | Testcontainers chạy PostgreSQL, RabbitMQ, Redis, MongoDB, Elasticsearch để test flow xuyên service. |
| Contract Testing | ✅ Bổ sung | Pact cho HTTP/gRPC service contract; JSON Schema contract cho RabbitMQ events. |
| API Documentation | ✅ Bổ sung | Swagger/OpenAPI sinh tự động từ ASP.NET annotations và NestJS Swagger decorators. Gateway publish unified OpenAPI. |

**Testing strategy:**
```
Unit tests:
  - Domain entity behavior
  - Command handler validation
  - Policy/RBAC/ABAC rules
  - RAG confidence calculation

Integration tests:
  - Create ticket -> outbox event -> RabbitMQ -> search projection
  - Add message idempotency replay
  - Refresh token rejected after membership removed
  - Article publish -> embedding job -> vector store

Contract tests:
  - Gateway -> support-service HTTP contract
  - identity-service -> workspace-service gRPC contract
  - RabbitMQ event schema compatibility
```

**CI quality gate:**
```
dotnet test --collect:"XPlat Code Coverage"
npm run test:cov
npm run lint
npm run openapi:check
npm run pact:verify
docker compose -f docker-compose.test.yml up --abort-on-container-exit
```

**API documentation rule:**
- Mỗi service publish OpenAPI tại `/swagger/v1/swagger.json`.
- Gateway gom docs tại `/api/docs`.
- Internal gRPC docs sinh từ `.proto` trong `libs/contracts/grpc`.
- Event contract docs sinh từ JSON Schema trong `libs/contracts/events`.

---

### 19.11 Final Compliance Summary

| Nhóm | Kết luận |
|------|----------|
| Nhóm 1 — Giao tiếp & Hạ tầng | ✅ Đủ sau khi bổ sung gRPC và request aggregation |
| Nhóm 2 — Dữ liệu & Tìm kiếm | ✅ Đủ sau khi bổ sung L1/L2 cache và Qdrant/pgvector |
| Nhóm 3 — File & Object Storage | ✅ Đủ sau khi bổ sung Cloudflare R2/Supabase Storage |
| Nhóm 4 — Bảo mật & Identity | ✅ Đủ sau khi bổ sung OAuth2/OIDC và ABAC |
| Nhóm 5 — Resilience & Patterns | ✅ Đủ sau khi bổ sung DDD, scoped Event Sourcing, Saga |
| Nhóm 6 — AI & LLM | ✅ Đủ sau khi bổ sung AI Agent/tool calling và Python ML service optional |
| Nhóm 7 — Notification & Integration | ✅ Đủ sau khi bổ sung push notification và webhook system |
| Nhóm 8 — Background Processing | ✅ Đủ |
| Nhóm 9 — Observability & DevOps | ✅ Đủ sau khi bổ sung health endpoint chi tiết và ELK/Seq option |
| Nhóm 10 — Testing & Documentation | ✅ Đủ sau khi bổ sung xUnit/Jest, Testcontainers, Pact, Swagger/OpenAPI |

**Điểm chốt khi trình bày:** SignalDesk AI không chỉ liệt kê công nghệ, mà đã gắn từng công nghệ vào service owner, data owner, failure mode, retry/idempotency strategy, observability metric và lý do chọn trong bối cảnh microservices.

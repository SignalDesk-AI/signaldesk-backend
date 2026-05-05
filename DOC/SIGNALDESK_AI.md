# SignalDesk AI
## Multi-tenant Customer Engagement, Support & Retention Platform

> **Đây là nguồn sự thật duy nhất (single source of truth) cho toàn bộ dự án SignalDesk AI.**
> Dùng để onboard context mới khi chat window bị reset, hoặc khi bắt đầu một task cụ thể.
> Phiên bản: `v3.0` — merged + production hardening cho concurrency, consistency, SLOs

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Product Vision & Người Dùng Mục Tiêu](#2-product-vision--người-dùng-mục-tiêu)
3. [Scope Management — Làm Gì, Không Làm Gì](#3-scope-management--làm-gì-không-làm-gì)
4. [Kiến Trúc Hệ Thống](#4-kiến-trúc-hệ-thống)
5. [Tech Stack & Lý Do Chọn](#5-tech-stack--lý-do-chọn)
6. [Danh Sách Services](#6-danh-sách-services)
7. [Cấu Trúc Repo & Folder Structure](#7-cấu-trúc-repo--folder-structure)
8. [Schema Database](#8-schema-database)
9. [Event Contracts](#9-event-contracts)
10. [Business Logic & Luồng Nghiệp Vụ](#10-business-logic--luồng-nghiệp-vụ)
11. [Patterns & Implementation Strategy](#11-patterns--implementation-strategy)
12. [AI Integration Strategy](#12-ai-integration-strategy)
13. [API Strategy](#13-api-strategy)
14. [Roadmap 10 Tuần](#14-roadmap-10-tuần)
15. [Definition of Done — CV-Ready Checklist](#15-definition-of-done--cv-ready-checklist)
16. [Metrics Cần Đo](#16-metrics-cần-đo)
17. [Portfolio & Interview Prep](#17-portfolio--interview-prep)
18. [Cut-Scope Guide Khi Bị Chậm](#18-cut-scope-guide-khi-bị-chậm)
19. [Context Handoff](#19-context-handoff)

---

## 1. Executive Summary

**SignalDesk AI** là nền tảng hỗ trợ khách hàng đa tenant dành cho SaaS/SME, kết hợp:

- customer support portal (public, SSR/ISR cho SEO)
- ticket & conversation management với full lifecycle
- knowledge base với versioning và publish workflow
- semantic search và hybrid retrieval
- agent/admin workspace (role-based)
- AI copilot: RAG chatbot + agent assist + auto-classify
- campaign/follow-up automation sau support
- analytics và observability production-ready

### 1.1 Bài Toán Giải Quyết

SME và SaaS startup thường bị phân mảnh dữ liệu:

- FAQ/knowledge base nằm một nơi
- ticket/support nằm nơi khác
- analytics hành vi người dùng không kết nối với support
- follow-up/campaign làm thủ công
- AI nếu có thì thường chỉ là chatbot rời rạc

SignalDesk AI gom các mảnh này thành một platform thống nhất để:

- khách hàng có thể tự tra cứu KB hoặc chat hỏi hỗ trợ
- AI trả lời dựa trên knowledge base của đúng tenant
- nếu AI không đủ tin tưởng, tự escalate thành ticket cho agent
- agent có AI hỗ trợ tóm tắt, gợi ý câu trả lời, tìm case tương tự
- admin quản lý KB, user, role, segment, campaign và dashboard

### 1.2 Tại Sao Project Này Mạnh Để Đưa Vào CV

- **Multi-tenant SaaS thực sự**: tenant isolation ở DB schema + cache + search + AI context
- **Event-driven với Outbox Pattern**: zero message loss, at-least-once delivery có thể prove được
- **RAG pipeline production-grade**: không chỉ "gọi ChatGPT" — có embedding pipeline, vector store, grounding, confidence-based fallback
- **Observability end-to-end**: trace một request xuyên nhiều service bằng Jaeger/Tempo
- **CQRS + Clean Architecture** trong service thực chiến, không chỉ lý thuyết
- **Production deployment**: CI/CD thật, VPS thật, uptime monitor thật

### 1.3 Mục Tiêu Nghề Nghiệp

Project này được thiết kế để:

- luyện kỹ năng backend/fullstack ở mức junior → middle → chạm ngưỡng senior-minded thinking
- có 1 project flagship đưa vào CV/portfolio
- tạo điểm nhấn khi phỏng vấn backend hoặc fullstack
- có đủ chất liệu để nói chuyện 30–45 phút về architecture, trade-off, performance, reliability, AI integration

### 1.4 Demo Tenants

Không cần customer thật. Tự tạo 2 tenant giả lập:

- **TaskFlow** — SaaS quản lý công việc/team collaboration
- **InvoiceFox** — SaaS quản lý billing/invoice

Mỗi tenant có: 20–30 KB articles, 50–100 FAQ, 80–150 tickets giả lập, 300–800 ticket messages, 20–30 câu hỏi test cho search/RAG.

---

## 2. Product Vision & Người Dùng Mục Tiêu

### 2.1 Người Dùng Mục Tiêu

| Role | Nhu cầu |
|------|---------|
| **Support Agent** | Xử lý ticket nhanh, nhận gợi ý AI, chat real-time với khách |
| **Team Lead / Manager** | Xem SLA, hiệu suất team, phân công tự động |
| **Tenant Admin / Owner** | Config KB, campaign, RBAC, AI rules, analytics |
| **Analyst** | Dashboard metrics, export data, theo dõi AI acceptance rate |
| **End Customer** | Chat widget, tự tra cứu KB portal, gửi ticket |
| **Super Admin** | Quản lý toàn bộ tenant, feature flags, billing mock |

### 2.2 Value Proposition

SignalDesk AI cung cấp workspace đa tenant nơi doanh nghiệp có thể:

- quản lý khách hàng và ticket với full audit trail
- quản lý knowledge base với publish workflow và versioning
- tìm kiếm theo từ khóa và theo ngữ nghĩa (hybrid search)
- dùng AI để tăng năng suất support (grounded, không hallucinate)
- tự động follow-up sau support để cải thiện retention

### 2.3 Các Use Case Chính

- khách hàng hỏi trên support widget → AI tìm KB và trả lời có citation
- nếu confidence thấp → AI tạo ticket cho agent thay vì bịa
- agent mở ticket → thấy AI summary + suggested reply sẵn
- admin publish article → hệ thống tự index vào search + RAG corpus
- khi ticket resolve → hệ thống gửi follow-up/survey tự động

---

## 3. Scope Management — Làm Gì, Không Làm Gì

### 3.1 Scope Nên Làm Trong 10 Tuần Đầu

- multi-tenant auth + RBAC
- ticket lifecycle đầy đủ (tạo → assign → reply → resolve → close)
- customer profile cơ bản
- knowledge base CRUD + publish + versioning
- **RabbitMQ** + Outbox Pattern (không phải Kafka ngay từ đầu — xem lý do ở section 5)
- search: keyword + semantic hybrid
- AI: ticket summary + suggested reply + RAG chatbot theo tenant
- dashboard metrics cơ bản
- observability cơ bản (logs, traces, metrics)
- deploy VPS bằng Docker Compose + Nginx + CI/CD

### 3.2 Scope Không Nên Làm Quá Sớm

- **Kafka** ngay từ đầu (RabbitMQ đủ dùng và dễ vận hành hơn cho dự án solo; migrate sang Kafka ở tuần 11–12 nếu có thời gian)
- Kubernetes quá sớm (Docker Compose đủ cho demo)
- true micro-frontend từ tuần đầu (shared packages đã đủ)
- full event sourcing (Outbox Pattern là đủ)
- complex saga orchestration
- advanced ML training pipeline
- Module Federation full-scale

### 3.3 Không Được Cắt Nếu Muốn Project Vẫn Mạnh

- tenant + RBAC
- support core (ticket lifecycle)
- KB + search
- RabbitMQ + Outbox Pattern
- AI summary/RAG v1 (đây là điểm mạnh nhất)
- deploy thật (có URL live)
- observability cơ bản (Grafana + traces)

---

## 4. Kiến Trúc Hệ Thống

### 4.1 High-Level Architecture

```
                         +----------------------+
                         |   Public Portal      |
                         |   Next.js SSR/ISR    |
                         |   (KB, Search, Help) |
                         +----------+-----------+
                                    |
                                    v
+------------------+      +----------------------+      +----------------------+
| Customer Widget  | ---> |   Gateway BFF        | <--- |  Workspace App       |
| Embeddable SDK   |      |   NestJS             |      |  Next.js             |
| (Next.js/iframe) |      +----------+-----------+      |  (Agent + Admin)     |
+------------------+                 |                  +----------------------+
                                     |
          -----------------------------------------------------------------------
          |             |              |              |          |         |
          v             v              v              v          v         v
  +---------------+ +-------------+ +-------------+ +--------+ +--------+ +------------------+
  | identity-svc  | | workspace   | | support-svc | | know-  | | search | | notification-svc |
  | ASP.NET Core  | | ASP.NET Core| | ASP.NET Core| | ledge  | | NestJS | | NestJS           |
  +-------+-------+ +------+------+ +------+------+ | svc    | +---+----+ +-------+----------+
          |                |               |         | ASP.NET|     |              |
          -------------------------------------------------------------------------
                                     |
                                     v
                              +----------------+
                              |   RabbitMQ     |
                              |   Event Bus    |
                              |   + Job Queue  |
                              +------+---------+
                                     |
                     ----------------------------------------
                     |                      |               |
                     v                      v               v
              +-------------+       +-------------+   +-------------+
              | ai-service  |       | campaign-svc|   | analytics   |
              | NestJS      |       | ASP.NET Core|   | (embedded   |
              +------+------+       +------+------+   |  in workspace|
                     |                     |           | -svc)       |
                     v                     v           +-------------+
         +---------------------+    +-------------+
         | Ollama / Cloud LLM  |    | PostgreSQL  |
         | Embeddings / RAG    |    | MongoDB     |
         +---------------------+    | Redis       |
                                    +-------------+

Infrastructure:
- Elasticsearch (search + vector)
- MinIO / S3 (file storage)
- OpenTelemetry → Jaeger/Tempo (traces)
- Prometheus → Grafana (metrics + alerts)
- Loki + Promtail (logs)
- Nginx (reverse proxy + SSL)
- GitHub Actions (CI/CD)
```

### 4.2 Communication Strategy

| Loại | Protocol | Dùng khi nào |
|------|----------|--------------|
| **Sync** | HTTP/REST | Client → Gateway → Service, action cần response ngay |
| **Internal Sync** | HTTP (internal DNS) | Service-to-service khi cần data ngay (auth check) |
| **Async** | RabbitMQ | Notification, AI enrichment, analytics, search indexing |
| **Real-time** | WebSocket (Socket.io) | Live chat, typing indicator, presence, live ticket update |

### 4.3 Tenant Isolation Strategy

```
Database level:
  - Một PostgreSQL instance, tách theo schema (identity / workspace / support / knowledge / ...)
  - tenant_id column trên mọi table
  - Application-level filter (không dùng RLS để ORM dễ hơn)
  - ASP.NET: ITenantContext injected vào repositories qua middleware

Cache level:
  - Redis keys bắt đầu bằng tenant_id
  - Không bao giờ cache cross-tenant
  - Ví dụ: cache:ticket:{tenantId}:{ticketId}

Elasticsearch level:
  - Mọi query đều có mandatory filter: { term: { tenant_id: "..." } }
  - Single index dùng chung, filter by tenant_id (không cần index riêng per tenant)

AI level:
  - RAG chỉ retrieve articles của tenant hiện tại
  - customer_memory indexed theo (tenant_id, customer_email)
  - System prompt include tenant context
```

---

## 5. Tech Stack & Lý Do Chọn

### 5.1 Frontend

| Tech | Lý do chọn |
|------|-----------|
| **Next.js 14** (App Router) | SSR cho KB portal (SEO), ISR cho articles, SSR cho workspace |
| **TypeScript** | Type safety, phỏng vấn luôn hỏi |
| **TanStack Query** | Server state, caching, optimistic updates |
| **Zustand** | UI state cục bộ (panel, filter, selection) — nhẹ hơn Redux |
| **Tailwind CSS + shadcn/ui** | Nhanh, đẹp, production-grade |
| **Socket.io-client** | WebSocket real-time |
| **TipTap** | WYSIWYG editor cho KB articles |
| **React Hook Form + Zod** | Forms + validation |
| **Vitest + Playwright** | Unit + E2E testing |
| **Turborepo** | Monorepo quản lý nhiều apps và packages |

**SSR/ISR Strategy:**
- `portal` (public KB): ISR (revalidate: 3600, on-demand revalidate khi article published)
- `workspace` (agent/admin): SSR (dynamic per user/tenant)
- `widget-sdk`: CSR (embeddable widget nhúng vào site ngoài)

### 5.2 Backend

| Tech | Lý do chọn |
|------|-----------|
| **ASP.NET Core 8** | Core business services — performance mạnh, typed domain model, validation, transactional logic, đẹp trong CV |
| **NestJS** | Gateway, notification, search, AI orchestration — nhanh để phát triển integration-heavy services, WebSocket, queue consumers |
| **Entity Framework Core 8** | ORM cho .NET services, migration dễ |
| **MediatR** | CQRS pattern cho .NET services |
| **Prisma / Mongoose** | ORM cho NestJS services |
| **FluentValidation** | Validation pipeline .NET |
| **Hangfire** | Background jobs, scheduled jobs trong .NET |
| **BullMQ** | Job queue trong NestJS |
| **Polly** | Circuit breaker, retry cho HTTP calls (.NET) |

### 5.3 Infrastructure & Data

| Tech | Lý do chọn |
|------|-----------|
| **PostgreSQL 16** | Core transactional store — ACID, schemas, JSONB |
| **MongoDB 7** | AI/context bán cấu trúc — ai_runs, customer_memory, conversation_summaries |
| **Redis 7** | Cache, session, rate limiting, presence, idempotency, distributed lock |
| **RabbitMQ** | Message broker giai đoạn đầu — phù hợp workflow business, dễ vận hành hơn Kafka cho solo dev, hỗ trợ DLQ tốt |
| **Elasticsearch 8** | Search engine chính — keyword search + dense_vector cho semantic search (hybrid) |
| **MinIO** | File storage S3-compatible cho attachments |
| **Docker + Docker Compose** | Local dev + production deploy |
| **Nginx** | Reverse proxy, SSL termination, gzip |
| **GitHub Actions** | CI/CD |

> **Lưu ý về Kafka:** Kafka sẽ được thêm vào tuần 11–12 (nếu có thời gian) để demo analytics stream hoặc khi throughput thực sự cần thiết. Không dùng Kafka từ đầu vì: (1) RabbitMQ đủ dùng cho scale của project; (2) Kafka phức tạp hơn để vận hành solo; (3) interviewer sẽ hỏi tại sao cần Kafka — phải có lý do rõ.

### 5.4 AI Stack

| Tech | Lý do chọn |
|------|-----------|
| **OpenAI GPT-4o-mini** | LLM primary — nhanh, rẻ, production-ready |
| **Ollama + llama3.2:3b** | LLM fallback tự-host, dev/offline — 2GB RAM |
| **OpenAI text-embedding-3-small** | Embeddings (1536 dims) |
| **nomic-embed-text via Ollama** | Embedding fallback (274MB) |
| **LangChain.js** | RAG orchestration trong ai-service |
| **Elasticsearch dense_vector** | Vector store — không cần Qdrant riêng, giảm complexity |
| **MongoDB** | Customer memory long-term, ai_runs logs |
| **Redis** | Short-term session context cache |

**AI dùng đúng chỗ:**
- support chatbot cho end-user (RAG)
- suggested reply cho agent
- conversation/ticket summary
- semantic search
- KB draft suggestion
- auto-classify ticket (category, priority, sentiment)

**AI không dùng cho:**
- SLA rules (dùng cron job so sánh deadline vs now)
- rate limiting (dùng Redis counter)
- assignment rule cơ bản (round-robin)
- auth/permission (deterministic)
- billing/business-critical logic

### 5.5 Observability

| Tech | Vai trò |
|------|---------|
| **Prometheus** | Metrics scraping |
| **Grafana** | Dashboards + alerts |
| **Jaeger / Tempo** | Distributed tracing (OpenTelemetry SDK) |
| **Loki + Promtail** | Log aggregation |
| **Serilog** | Structured logging (.NET) |
| **Winston** | Structured logging (NestJS) |
| **Sentry** | Error tracking (FE + BE) |
| **UptimeRobot** | Uptime monitoring (free) |

---

## 6. Danh Sách Services

### Application Services

| Service | Tech | Port | Trách nhiệm |
|---------|------|------|-------------|
| `gateway-bff` | NestJS | 3000 | Auth propagation, routing, rate limit, tenant resolution, WebSocket endpoint, correlation ID |
| `identity-service` | ASP.NET Core 8 | 5001 | User registration/login, refresh token, password reset, email verification |
| `workspace-service` | ASP.NET Core 8 | 5002 | Tenant provisioning, membership, role/permission, feature flags, usage limits |
| `support-service` | ASP.NET Core 8 | 5003 | Customer profile, ticket lifecycle, assignment, SLA, internal notes, audit timeline |
| `knowledge-service` | ASP.NET Core 8 | 5004 | Article CRUD, versioning, publish/unpublish, file metadata |
| `notification-service` | NestJS | 3001 | Email, in-app notification, WebSocket push, retries, DLQ handling |
| `search-service` | NestJS | 3002 | Consume indexing events, sync Elasticsearch, unified search endpoint (keyword + semantic + hybrid) |
| `ai-service` | NestJS | 3003 | RAG pipeline, suggested reply, summarization, memory snapshot, AI run logs |
| `campaign-service` | ASP.NET Core 8 | 5005 | Segment definition, campaign scheduling, follow-up workflow _(optional, tuần 9)_ |

**Quy tắc gateway:**
- Không nhét business logic cốt lõi vào gateway
- Không lưu domain data riêng
- Mọi request đi qua gateway đều được gắn `X-Tenant-Id` và `X-Correlation-Id`

### Infrastructure Services (Docker)

| Service | Port | Dùng bởi |
|---------|------|---------|
| PostgreSQL 16 | 5432 | tất cả .NET services (multi-schema) |
| MongoDB 7 | 27017 | ai-service, notification-service |
| Redis 7 | 6379 | gateway, tất cả services |
| RabbitMQ 3.13 | 5672 / 15672 (UI) | event bus cho mọi service |
| Elasticsearch 8 | 9200 | search-service, ai-service |
| MinIO | 9000 / 9001 (console) | knowledge-service (file attachments) |
| Prometheus | 9090 | metrics scraping |
| Grafana | 3100 | dashboards |
| Jaeger / Tempo | 16686 / 3200 | distributed tracing |
| Loki | 3300 | log aggregation |
| Mailpit | 1025 / 8025 (UI) | dev email testing (thay Mailtrap) |
| Ollama | 11434 | ai-service (dev fallback) |

---

## 7. Cấu Trúc Repo & Folder Structure

### 2 Repo Riêng Biệt

```
GitHub Organization: signaldesk-ai/
├── signaldesk-fe/   ← Turborepo monorepo (apps + packages)
└── signaldesk-be/   ← NX monorepo (services + libs)
```

**Tại sao tách 2 repo:**
- FE/BE có CI/CD riêng, deploy cadence khác nhau
- dễ quản lý context công việc
- phù hợp khi muốn demo "real team setup"

---

### Repo 1: `signaldesk-fe`

```
signaldesk-fe/
├── apps/
│   ├── portal/                        ← Next.js: public help center, KB pages, SSR/ISR
│   │   ├── app/
│   │   │   ├── (public)/
│   │   │   │   ├── page.tsx           ← Landing / help center home
│   │   │   │   ├── help/
│   │   │   │   │   └── [slug]/page.tsx  ← KB article (ISR)
│   │   │   │   └── search/page.tsx    ← Public search
│   │   │   ├── login/page.tsx
│   │   │   └── api/
│   │   │       └── revalidate/route.ts  ← On-demand ISR revalidation
│   │   ├── components/
│   │   ├── public/
│   │   └── next.config.ts
│   │
│   ├── workspace/                     ← Next.js: agent + admin console (SSR)
│   │   ├── app/
│   │   │   ├── (auth)/
│   │   │   │   ├── login/page.tsx
│   │   │   │   └── layout.tsx
│   │   │   ├── (app)/
│   │   │   │   ├── layout.tsx         ← Sidebar, header, socket provider
│   │   │   │   ├── tickets/
│   │   │   │   │   ├── page.tsx       ← Ticket list (SSR, filters)
│   │   │   │   │   └── [id]/page.tsx  ← Ticket detail + AI panel
│   │   │   │   ├── customers/page.tsx
│   │   │   │   ├── chat/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   └── [sessionId]/page.tsx
│   │   │   │   ├── kb/
│   │   │   │   │   ├── page.tsx       ← Article list
│   │   │   │   │   ├── [id]/edit/page.tsx  ← TipTap editor
│   │   │   │   │   └── new/page.tsx
│   │   │   │   ├── campaigns/page.tsx
│   │   │   │   ├── analytics/page.tsx ← Recharts dashboards
│   │   │   │   └── settings/
│   │   │   │       ├── team/page.tsx
│   │   │   │       ├── roles/page.tsx
│   │   │   │       └── ai-config/page.tsx
│   │   │   └── api/
│   │   │       └── auth/[...nextauth]/route.ts
│   │   ├── components/
│   │   │   ├── ticket/
│   │   │   │   ├── TicketCard.tsx
│   │   │   │   ├── TicketTimeline.tsx
│   │   │   │   ├── ReplyEditor.tsx
│   │   │   │   └── AISuggestPanel.tsx
│   │   │   ├── chat/
│   │   │   │   ├── ChatWindow.tsx
│   │   │   │   ├── MessageBubble.tsx
│   │   │   │   └── TypingIndicator.tsx
│   │   │   └── analytics/
│   │   │       ├── TicketVolumeChart.tsx
│   │   │       ├── AgentPerformanceTable.tsx
│   │   │       └── AIDeflectionWidget.tsx
│   │   ├── hooks/
│   │   │   ├── useSocket.ts
│   │   │   ├── useTickets.ts
│   │   │   └── useAISuggest.ts
│   │   ├── store/                     ← Zustand stores
│   │   │   ├── authStore.ts
│   │   │   ├── ticketStore.ts
│   │   │   └── chatStore.ts
│   │   └── next.config.ts
│   │
│   └── widget-sdk/                    ← Embeddable chat widget
│       ├── src/
│       │   ├── embed/
│       │   │   └── index.ts           ← <script> snippet loader
│       │   ├── widget/
│       │   │   ├── ChatWidget.tsx
│       │   │   ├── MessageList.tsx
│       │   │   └── InputBar.tsx
│       │   └── api/
│       │       └── widgetClient.ts
│       └── package.json
│
├── packages/
│   ├── ui/                            ← Shared component library (shadcn/ui base)
│   │   └── src/components/
│   │       ├── Button/
│   │       ├── Badge/
│   │       ├── DataTable/
│   │       └── index.ts
│   │
│   ├── api-client/                    ← Generated/manual API client
│   │   └── src/
│   │       ├── tickets.ts
│   │       ├── knowledge.ts
│   │       ├── ai.ts
│   │       └── index.ts
│   │
│   ├── types/                         ← Shared TypeScript types
│   │   └── src/
│   │       ├── ticket.types.ts
│   │       ├── user.types.ts
│   │       ├── tenant.types.ts
│   │       └── index.ts
│   │
│   ├── auth/                          ← Auth helpers, guards, permission hooks
│   │   └── src/
│   │       ├── guards/
│   │       ├── session/
│   │       └── permissions/
│   │
│   ├── realtime/                      ← WebSocket hooks
│   │   └── src/
│   │       ├── socket/
│   │       ├── presence/
│   │       └── notifications/
│   │
│   └── config/                        ← Shared ESLint, Tailwind, TS config
│
├── turbo.json
├── package.json
└── .github/
    └── workflows/
        ├── ci.yml                     ← lint + test + build trên PR
        ├── deploy-portal.yml
        └── deploy-workspace.yml
```

---

### Repo 2: `signaldesk-be`

```
signaldesk-be/
├── services/
│   ├── gateway-bff/                   ← NestJS
│   │   └── src/
│   │       ├── middleware/
│   │       │   ├── auth.middleware.ts      ← JWT verify + tenant extract
│   │       │   ├── rate-limit.middleware.ts
│   │       │   └── correlation.middleware.ts
│   │       ├── proxy/
│   │       │   ├── ticket.proxy.ts
│   │       │   ├── knowledge.proxy.ts
│   │       │   └── ai.proxy.ts
│   │       ├── guards/
│   │       ├── websocket/             ← WebSocket gateway forward
│   │       └── app.module.ts
│   │
│   ├── identity-service/              ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── IdentityService.Domain/
│   │       │   ├── Entities/           (User, RefreshToken)
│   │       │   └── Events/             (UserRegisteredEvent)
│   │       ├── IdentityService.Application/
│   │       │   ├── Commands/           (RegisterUserCommand, LoginCommand)
│   │       │   └── Queries/
│   │       ├── IdentityService.Infrastructure/
│   │       │   ├── Persistence/        (EF Core, Migrations)
│   │       │   └── Messaging/          (OutboxPublisher)
│   │       └── IdentityService.API/
│   │           └── Controllers/
│   │
│   ├── workspace-service/             ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── WorkspaceService.Domain/
│   │       │   ├── Entities/           (Tenant, Membership, Role, Permission, FeatureFlag)
│   │       │   └── Events/             (TenantCreatedEvent, MemberInvitedEvent)
│   │       ├── WorkspaceService.Application/
│   │       ├── WorkspaceService.Infrastructure/
│   │       └── WorkspaceService.API/
│   │
│   ├── support-service/               ← ASP.NET Core 8 (Clean Architecture + CQRS)
│   │   └── src/
│   │       ├── SupportService.Domain/
│   │       │   ├── Entities/           (Customer, Ticket, TicketMessage, SlaPolicy, Team)
│   │       │   ├── ValueObjects/       (TicketStatus, Priority)
│   │       │   └── Events/             (TicketCreated, Assigned, Resolved)
│   │       ├── SupportService.Application/
│   │       │   ├── Commands/           (CreateTicket, AssignTicket, AddMessage, ResolveTicket)
│   │       │   ├── Queries/            (GetTickets, GetTicketById, GetTicketStats)
│   │       │   └── Handlers/
│   │       ├── SupportService.Infrastructure/
│   │       │   ├── Persistence/        (AppDbContext, Repositories, Migrations)
│   │       │   └── Messaging/          (OutboxPublisher, RabbitMQ producer)
│   │       └── SupportService.API/
│   │           └── Controllers/        (TicketsController, CustomersController)
│   │
│   ├── knowledge-service/             ← ASP.NET Core 8 (Clean Architecture)
│   │   └── src/
│   │       ├── KnowledgeService.Domain/
│   │       │   ├── Entities/           (Article, Category, ArticleVersion)
│   │       │   └── Events/             (ArticlePublishedEvent)
│   │       ├── KnowledgeService.Application/
│   │       ├── KnowledgeService.Infrastructure/
│   │       │   ├── Persistence/
│   │       │   └── Messaging/          (OutboxPublisher)
│   │       └── KnowledgeService.API/
│   │
│   ├── notification-service/          ← NestJS
│   │   └── src/
│   │       ├── consumers/
│   │       │   ├── ticket-events.consumer.ts
│   │       │   └── campaign-events.consumer.ts
│   │       ├── transports/
│   │       │   ├── email.transport.ts  (Nodemailer + Mailpit dev)
│   │       │   └── inapp.transport.ts
│   │       └── providers/
│   │           └── template.service.ts (MongoDB templates)
│   │
│   ├── search-service/                ← NestJS
│   │   └── src/
│   │       ├── consumers/
│   │       │   ├── article-indexer.consumer.ts
│   │       │   └── ticket-indexer.consumer.ts
│   │       ├── indexing/
│   │       │   └── elasticsearch.service.ts
│   │       └── retrieval/
│   │           └── search.service.ts   (hybrid keyword + kNN)
│   │
│   ├── ai-service/                    ← NestJS
│   │   └── src/
│   │       ├── rag/
│   │       │   ├── rag.service.ts
│   │       │   └── retriever.service.ts   (ES kNN)
│   │       ├── classify/
│   │       │   └── intent.service.ts
│   │       ├── suggest/
│   │       │   └── suggest.service.ts
│   │       ├── memory/
│   │       │   └── memory.service.ts      (MongoDB long-term)
│   │       └── consumers/
│   │           ├── ticket-created.consumer.ts
│   │           └── article-published.consumer.ts
│   │
│   └── campaign-service/              ← ASP.NET Core 8 (optional)
│       └── src/
│           ├── CampaignService.Domain/
│           └── CampaignService.Application/
│               └── Jobs/               (Hangfire scheduled jobs)
│
├── libs/
│   ├── contracts/
│   │   └── events/                    ← Event schemas (JSON Schema / TypeScript)
│   ├── dotnet/
│   │   └── BuildingBlocks/
│   │       ├── Common/                ← Result<T>, base Entity, ITenantContext
│   │       ├── Messaging/             ← RabbitMQ producer abstractions
│   │       ├── Persistence/           ← Outbox base, soft delete, audit
│   │       └── Observability/         ← OTel setup, structured logging
│   └── node/
│       └── common/
│           ├── config/
│           ├── logger/                ← Winston + OTel
│           ├── messaging/             ← RabbitMQ client helpers
│           └── tracing/
│
├── infra/
│   ├── docker/
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.dev.yml
│   │   └── docker-compose.infra.yml  ← Chỉ infra (PG, Mongo, Redis, RabbitMQ...)
│   ├── nginx/
│   │   ├── nginx.conf
│   │   └── conf.d/
│   │       ├── gateway.conf
│   │       └── frontend.conf
│   └── observability/
│       ├── prometheus/prometheus.yml
│       ├── grafana/dashboards/
│       ├── loki/loki-config.yaml
│       └── otel-collector/otel-config.yaml
│
└── .github/
    └── workflows/
        ├── ci.yml
        └── deploy-production.yml
```

---

## 8. Schema Database

### PostgreSQL Strategy

Dùng một PostgreSQL instance `signaldesk`, tách theo schema để giữ logical boundary theo service, dễ local dev, và dễ migrate sang nhiều DB riêng khi cần.

**Schemas:**
- `identity` — users, refresh_tokens
- `workspace` — tenants, memberships, roles, permissions, feature_flags
- `support` — customers, tickets, ticket_messages, ticket_status_history, sla_policies
- `knowledge` — articles, article_versions, categories, files
- `notification` — notifications, deliveries, templates
- `campaign` — campaigns, segments, send_history
- `ops` — outbox_events, inbox_messages (dedup), audit_logs

---

#### Schema `identity`

```sql
CREATE TABLE identity.users (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email           VARCHAR(255) NOT NULL UNIQUE,
  password_hash   VARCHAR(255) NOT NULL,
  display_name    VARCHAR(255) NOT NULL,
  avatar_url      VARCHAR(500),
  status          VARCHAR(50)  NOT NULL DEFAULT 'Pending', -- Pending|Active|Suspended
  email_verified  BOOLEAN NOT NULL DEFAULT false,
  last_login_at   TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE identity.refresh_tokens (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
  token_hash  VARCHAR(255) NOT NULL UNIQUE,
  device_id   VARCHAR(255),
  expires_at  TIMESTAMPTZ NOT NULL,
  revoked_at  TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_user ON identity.refresh_tokens(user_id);
```

---

#### Schema `workspace`

```sql
CREATE TABLE workspace.tenants (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  slug        VARCHAR(100) NOT NULL UNIQUE,  -- dùng cho subdomain
  name        VARCHAR(255) NOT NULL,
  plan_code   VARCHAR(50)  NOT NULL DEFAULT 'Starter', -- Starter|Pro|Enterprise
  status      VARCHAR(50)  NOT NULL DEFAULT 'Trial',   -- Trial|Active|Suspended
  settings    JSONB DEFAULT '{}',
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE workspace.memberships (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL REFERENCES workspace.tenants(id) ON DELETE CASCADE,
  user_id     UUID NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
  role_code   VARCHAR(50) NOT NULL,  -- Owner|Admin|Manager|Agent|Analyst|Viewer
  status      VARCHAR(50) NOT NULL DEFAULT 'Invited', -- Invited|Active|Disabled
  invited_by  UUID REFERENCES identity.users(id),
  joined_at   TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, user_id)
);

CREATE TABLE workspace.permissions (
  id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  action  VARCHAR(100) NOT NULL UNIQUE  -- tickets:create|kb:publish|...
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

CREATE INDEX idx_memberships_tenant ON workspace.memberships(tenant_id);
CREATE INDEX idx_memberships_user ON workspace.memberships(user_id);
```

---

#### Schema `support`

```sql
CREATE TABLE support.customers (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  external_ref    VARCHAR(255),
  email           VARCHAR(255) NOT NULL,
  name            VARCHAR(255),
  lifecycle_stage VARCHAR(50) DEFAULT 'Active', -- Lead|Active|AtRisk|Churned
  tags            JSONB DEFAULT '[]',
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, email)
);

CREATE TABLE support.sla_policies (
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id             UUID NOT NULL,
  name                  VARCHAR(255) NOT NULL,
  priority              VARCHAR(50) NOT NULL,
  first_response_mins   INTEGER NOT NULL,
  resolution_mins       INTEGER NOT NULL,
  business_hours_only   BOOLEAN DEFAULT false,
  is_active             BOOLEAN DEFAULT true
);

CREATE TABLE support.ticket_counters (
  tenant_id     UUID PRIMARY KEY,
  last_value    BIGINT NOT NULL DEFAULT 0,
  updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE support.tickets (
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           UUID NOT NULL,
  customer_id         UUID NOT NULL REFERENCES support.customers(id),
  ticket_no           VARCHAR(20) NOT NULL,          -- TK-000123, unique per tenant
  subject             VARCHAR(500) NOT NULL,
  status              VARCHAR(50) NOT NULL DEFAULT 'Open',
  -- Open|InProgress|WaitingCustomer|Resolved|Closed
  priority            VARCHAR(50) NOT NULL DEFAULT 'Medium',
  -- Low|Medium|High|Urgent
  category            VARCHAR(100),
  tags                VARCHAR(100)[],
  channel             VARCHAR(50) DEFAULT 'Widget',  -- Widget|Email|Portal|API
  assigned_agent_id   UUID,
  team_id             UUID,
  sla_policy_id       UUID REFERENCES support.sla_policies(id),
  sla_deadline        TIMESTAMPTZ,
  sla_breached        BOOLEAN DEFAULT false,
  first_response_at   TIMESTAMPTZ,
  resolved_at         TIMESTAMPTZ,
  closed_at           TIMESTAMPTZ,
  ai_summary          TEXT,                          -- AI-generated summary
  metadata            JSONB DEFAULT '{}',
  row_version         BIGINT NOT NULL DEFAULT 0,     -- optimistic concurrency token
  created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, ticket_no)
);

CREATE INDEX idx_tickets_tenant_status ON support.tickets(tenant_id, status);
CREATE INDEX idx_tickets_tenant_assigned ON support.tickets(tenant_id, assigned_agent_id);
CREATE INDEX idx_tickets_tenant_created ON support.tickets(tenant_id, created_at DESC);
CREATE INDEX idx_tickets_sla ON support.tickets(sla_deadline) WHERE sla_breached = false;

CREATE TABLE support.ticket_messages (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  ticket_id       UUID NOT NULL REFERENCES support.tickets(id) ON DELETE CASCADE,
  author_type     VARCHAR(50) NOT NULL,  -- Customer|Agent|System|AI
  author_id       UUID,
  body_text       TEXT NOT NULL,
  content_type    VARCHAR(50) DEFAULT 'text',  -- text|html|markdown
  is_internal_note BOOLEAN DEFAULT false,
  attachments     JSONB DEFAULT '[]',
  source          VARCHAR(50) DEFAULT 'manual',
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_ticket_messages_ticket ON support.ticket_messages(ticket_id, created_at);

CREATE TABLE support.ticket_status_history (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL,
  ticket_id   UUID NOT NULL REFERENCES support.tickets(id),
  from_status VARCHAR(50),
  to_status   VARCHAR(50) NOT NULL,
  changed_by  UUID,
  note        TEXT,
  changed_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE support.teams (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL,
  name        VARCHAR(255) NOT NULL,
  description TEXT,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

#### Schema `knowledge`

```sql
CREATE TABLE knowledge.categories (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL,
  name        VARCHAR(255) NOT NULL,
  slug        VARCHAR(255) NOT NULL,
  parent_id   UUID REFERENCES knowledge.categories(id),
  sort_order  INTEGER DEFAULT 0,
  is_public   BOOLEAN DEFAULT true,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, slug)
);

CREATE TABLE knowledge.articles (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  category_id     UUID REFERENCES knowledge.categories(id),
  title           VARCHAR(500) NOT NULL,
  slug            VARCHAR(500) NOT NULL,
  summary         TEXT,
  status          VARCHAR(50) DEFAULT 'Draft', -- Draft|Published|Archived
  visibility      VARCHAR(50) DEFAULT 'Public', -- Public|Internal|Private
  current_version INTEGER NOT NULL DEFAULT 1,
  author_id       UUID NOT NULL,
  view_count      INTEGER DEFAULT 0,
  helpful_count   INTEGER DEFAULT 0,
  unhelpful_count INTEGER DEFAULT 0,
  tags            VARCHAR(100)[],
  meta_description VARCHAR(500),
  published_at    TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE(tenant_id, slug)
);

CREATE INDEX idx_articles_tenant_status ON knowledge.articles(tenant_id, status);
CREATE INDEX idx_articles_category ON knowledge.articles(category_id);

CREATE TABLE knowledge.article_versions (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  article_id  UUID NOT NULL REFERENCES knowledge.articles(id),
  version_no  INTEGER NOT NULL,
  body_md     TEXT NOT NULL,           -- Markdown source
  body_html   TEXT NOT NULL,           -- Rendered HTML
  body_plain  TEXT NOT NULL,           -- Plain text for search/embedding
  checksum    VARCHAR(64),
  created_by  UUID NOT NULL,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE knowledge.files (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    UUID NOT NULL,
  storage_key  VARCHAR(500) NOT NULL,  -- MinIO object key
  filename     VARCHAR(255) NOT NULL,
  mime_type    VARCHAR(100),
  size_bytes   BIGINT,
  checksum     VARCHAR(64),
  status       VARCHAR(50) DEFAULT 'Uploaded',
  uploaded_by  UUID NOT NULL,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

#### Schema `notification`

```sql
CREATE TABLE notification.notifications (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL,
  user_id     UUID,
  type        VARCHAR(100) NOT NULL, -- TicketAssigned|TicketResolved|Mention|...
  title       VARCHAR(500) NOT NULL,
  body        TEXT,
  channel     VARCHAR(50) NOT NULL,  -- Email|InApp|WebSocket
  status      VARCHAR(50) NOT NULL DEFAULT 'Pending',
  dedupe_key  VARCHAR(255) UNIQUE,   -- tránh duplicate notification
  read_at     TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE notification.deliveries (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  notification_id UUID NOT NULL REFERENCES notification.notifications(id),
  provider        VARCHAR(100) NOT NULL,  -- smtp|resend|internal
  status          VARCHAR(50) NOT NULL DEFAULT 'Pending',
  attempts        INTEGER DEFAULT 0,
  last_error      TEXT,
  sent_at         TIMESTAMPTZ
);
```

---

#### Schema `ops` — Shared Infrastructure Tables

```sql
-- Outbox: mỗi service write vào đây trong cùng transaction với domain change
CREATE TABLE ops.outbox_events (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  service_name    VARCHAR(100) NOT NULL,  -- support-service|knowledge-service|...
  aggregate_type  VARCHAR(100) NOT NULL,
  aggregate_id    UUID NOT NULL,
  event_type      VARCHAR(255) NOT NULL,
  payload         JSONB NOT NULL,
  headers         JSONB DEFAULT '{}',
  status          VARCHAR(50) NOT NULL DEFAULT 'pending', -- pending|processing|published|dead_letter
  retry_count     INTEGER DEFAULT 0,
  next_retry_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  claimed_by      VARCHAR(100),
  claimed_at      TIMESTAMPTZ,
  last_error      TEXT,
  occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  published_at    TIMESTAMPTZ
);

CREATE INDEX idx_outbox_pending ON ops.outbox_events(next_retry_at, occurred_at) WHERE status = 'pending';
CREATE INDEX idx_outbox_service ON ops.outbox_events(service_name, status);

-- Inbox: deduplication cho consumers
CREATE TABLE ops.inbox_messages (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  consumer_name VARCHAR(100) NOT NULL,
  event_id      UUID NOT NULL,
  status        VARCHAR(50) NOT NULL DEFAULT 'Processing', -- Processing|Done|Failed
  processed_at  TIMESTAMPTZ,
  error         TEXT,
  UNIQUE(consumer_name, event_id)   -- idempotency
);

-- Audit log toàn hệ thống
CREATE TABLE ops.audit_logs (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID,
  actor_type      VARCHAR(50) NOT NULL DEFAULT 'User', -- User|System|AI
  actor_id        UUID,
  action          VARCHAR(100) NOT NULL,
  entity_type     VARCHAR(100) NOT NULL,
  entity_id       UUID NOT NULL,
  before_data     JSONB,
  after_data      JSONB,
  correlation_id  UUID,
  ip_address      INET,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_entity ON ops.audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_tenant ON ops.audit_logs(tenant_id, created_at DESC);
```

---

### MongoDB Collections

#### Database `ai_db` — AI Service

```javascript
// ai_runs — log mọi AI inference (không lưu full input/output để giảm cost)
{
  _id: ObjectId,
  tenant_id: "uuid",
  feature: "rag_chatbot|agent_assist|classify|semantic_search|summary",
  source_type: "chat_session|ticket",
  source_id: "uuid",
  model_used: "gpt-4o-mini|llama3.2",
  latency_ms: 1250,
  tokens_used: { prompt: 800, completion: 200 },
  retrieved_chunks: 3,
  confidence_score: 0.87,
  feedback: "accepted|rejected|ignored|none",
  created_at: ISODate
}

// customer_memory_snapshots — long-term memory per customer
{
  _id: ObjectId,
  tenant_id: "uuid",
  customer_email: "string",
  history_summary: "AI-generated summary of interaction history",
  issue_patterns: ["billing", "authentication"],
  resolution_patterns: ["password_reset", "refund_approved"],
  preferences: { language: "vi", communication_tone: "formal" },
  interaction_count: 42,
  satisfaction_score_avg: 4.2,
  last_interaction: ISODate,
  updated_at: ISODate
}

// conversation_summaries — tóm tắt per ticket/session
{
  _id: ObjectId,
  tenant_id: "uuid",
  source_type: "ticket|chat_session",
  source_id: "uuid",
  summary: "text",
  key_points: ["array of strings"],
  sentiment: "positive|neutral|negative",
  resolution_code: "string",
  created_at: ISODate
}

// Indexes
db.customer_memory_snapshots.createIndex({ tenant_id: 1, customer_email: 1 }, { unique: true })
db.ai_runs.createIndex({ tenant_id: 1, created_at: -1 })
```

#### Database `chat_db` — Chat Widget Sessions

```javascript
// chat_sessions
{
  _id: ObjectId,
  session_id: "uuid",
  tenant_id: "uuid",
  ticket_id: "uuid",          // null nếu chưa escalate
  customer_email: "string",
  customer_name: "string",
  status: "active|closed|waiting_agent",
  channel: "widget|email|api",
  messages: [
    {
      message_id: "uuid",
      role: "customer|agent|bot|system",
      sender_id: "uuid",
      content: "string",
      ai_generated: false,
      sources: [],             // citations nếu AI trả lời
      timestamp: ISODate
    }
  ],
  metadata: { user_agent: "string", page_url: "string" },
  created_at: ISODate,
  updated_at: ISODate
}

db.chat_sessions.createIndex({ tenant_id: 1, customer_email: 1 })
db.chat_sessions.createIndex({ session_id: 1 }, { unique: true })
db.chat_sessions.createIndex({ ticket_id: 1 })
```

---

### Redis Key Patterns

```
# Auth / Session
session:{userId}                          → JWT payload cache          TTL: 900s
refresh_token:{tokenHash}                 → userId                     TTL: 604800s
user_permissions:{userId}:{tenantId}      → permission set             TTL: 3600s

# Rate Limiting
ratelimit:{tenantId}:{endpoint}:{min}     → request count              TTL: 60s
ratelimit:auth:login:{ip}                 → attempt count              TTL: 300s

# Cache (cache-aside)
cache:ticket:{tenantId}:{ticketId}        → ticket JSON                TTL: 300s
cache:tickets-list:{tenantId}:{hash}      → paginated result           TTL: 60s
cache:kb:{tenantId}:{slug}                → article JSON               TTL: 3600s

# WebSocket / Presence
presence:{tenantId}:{userId}              → "online"                   TTL: 30s (heartbeat)
chat:lock:ticket:{ticketId}               → agentId:lockToken          TTL: 30s

# AI Cache
ai:suggest:{ticketId}                     → suggestion JSON            TTL: 300s
ai:classify:{hash(content)}              → classification result      TTL: 3600s

# Advisory / Scheduler Lock
lock:campaign:send:{campaignId}          → workerId:lockToken         TTL: 3600s

# Idempotency
idempotency:{tenantId}:{userId}:{hash}    → response JSON              TTL: 86400s
```

---

### Elasticsearch Indices

```json
// tickets_v1
{
  "mappings": {
    "properties": {
      "ticket_id":      { "type": "keyword" },
      "tenant_id":      { "type": "keyword" },
      "ticket_no":      { "type": "keyword" },
      "subject":        { "type": "text", "analyzer": "standard" },
      "status":         { "type": "keyword" },
      "priority":       { "type": "keyword" },
      "category":       { "type": "keyword" },
      "tags":           { "type": "keyword" },
      "customer_email": { "type": "keyword" },
      "assigned_agent_id": { "type": "keyword" },
      "ai_summary":     { "type": "text" },
      "created_at":     { "type": "date" },
      "resolved_at":    { "type": "date" }
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
      "embedding":    { "type": "dense_vector", "dims": 1536, "index": true, "similarity": "cosine" },
      "published_at": { "type": "date" }
    }
  }
}
```

---

## 9. Event Contracts

### 9.1 Phân Biệt Domain Event vs Job Message

**Domain event** — mô tả một việc đã xảy ra trong domain. Broadcast ra nhiều consumers.

Ví dụ: `support.ticket.created.v1`, `knowledge.article.published.v1`

**Job message** — command cho một worker làm một việc cụ thể.

Ví dụ: `notification.send-email.v1`, `search.reindex-document.v1`

### 9.2 Event Envelope Chuẩn

```json
{
  "eventId": "uuid",
  "eventType": "support.ticket.created.v1",
  "occurredAt": "2026-04-20T10:00:00Z",
  "producer": "support-service",
  "tenantId": "uuid",
  "correlationId": "uuid",
  "causationId": "uuid",
  "aggregate": {
    "type": "ticket",
    "id": "uuid",
    "version": 1
  },
  "actor": {
    "type": "user",
    "id": "uuid"
  },
  "payload": {}
}
```

### 9.3 Domain Events — v1

#### `workspace.tenant.created.v1`
```json
{ "tenantId": "uuid", "slug": "taskflow", "ownerUserId": "uuid" }
```
**Consumers:** notification-service (welcome email), audit-log

#### `workspace.member.invited.v1`
```json
{ "tenantId": "uuid", "email": "agent@taskflow.io", "roleCode": "Agent", "invitedBy": "uuid" }
```
**Consumers:** notification-service

#### `support.customer.upserted.v1`
```json
{ "customerId": "uuid", "tenantId": "uuid", "email": "customer@example.com", "name": "Jane Doe", "tags": ["trial"] }
```
**Consumers:** search-service, campaign-service

#### `support.ticket.created.v1`
```json
{
  "ticketId": "uuid", "ticketNo": "TK-000123",
  "customerId": "uuid", "customerEmail": "customer@example.com",
  "subject": "Cannot access workspace",
  "priority": "High", "channel": "Widget",
  "source": "widget|email|api|manual"
}
```
**Consumers:** notification-service, search-service, ai-service

#### `support.ticket.assigned.v1`
```json
{ "ticketId": "uuid", "assignedAgentId": "uuid", "previousAgentId": "uuid", "assignedBy": "uuid" }
```
**Consumers:** notification-service

#### `support.ticket.message-added.v1`
```json
{ "ticketId": "uuid", "messageId": "uuid", "authorType": "Customer", "isInternalNote": false }
```
**Consumers:** search-service, ai-service

#### `support.ticket.resolved.v1`
```json
{
  "ticketId": "uuid", "customerId": "uuid", "customerEmail": "string",
  "resolvedBy": "uuid", "resolvedAt": "ISO8601",
  "resolutionCode": "RESOLVED|WORKAROUND|DUPLICATE",
  "aiAssisted": true, "resolutionTimeMinutes": 42, "slaMet": true
}
```
**Consumers:** notification-service, ai-service, campaign-service, search-service

#### `knowledge.article.published.v1`
```json
{
  "articleId": "uuid", "tenantId": "uuid", "slug": "how-to-reset-password",
  "version": 3, "title": "How to reset password",
  "bodyPlain": "...",
  "visibility": "Public"
}
```
**Consumers:** search-service (index + embed), ai-service (update corpus), portal (revalidate ISR)

#### `ai.classification.completed.v1`
```json
{
  "ticketId": "uuid",
  "category": "Billing", "prioritySuggestion": "High",
  "sentiment": "frustrated", "tags": ["billing", "urgent"],
  "confidence": 0.89
}
```
**Consumers:** support-service (update ticket)

#### `ai.summary.completed.v1`
```json
{ "sourceType": "ticket", "sourceId": "uuid", "summaryId": "uuid", "confidence": 0.91 }
```
**Consumers:** support-service, search-service

### 9.4 Job Messages

- `notification.send-email.v1`
- `notification.push-inapp.v1`
- `search.reindex-document.v1`
- `ai.generate-summary.v1`
- `ai.generate-reply-draft.v1`
- `campaign.dispatch.v1`

### 9.5 Reliability Rules

- mọi publish event phải đi qua Outbox Pattern (write trong cùng DB transaction)
- mọi consumer phải check `ops.inbox_messages` để deduplication (Inbox Pattern)
- retry tối đa 5 lần, sau đó chuyển Dead Letter Queue
- idempotency key bắt buộc với operations có thể retry
- event payload không được breaking change — dùng versioning hậu tố `.v1`, `.v2`
- `correlationId` truyền qua toàn bộ chain để trace

---

## 10. Business Logic & Luồng Nghiệp Vụ

### Luồng 1: Customer Tạo Ticket Qua Chat Widget

```
Customer mở chat widget
    │
    ▼
chat-session tạo mới (MongoDB)
ai-service nhận event: tìm KB → build RAG prompt
    │
    ├─ confidence ≥ 0.75 → AI trả lời + citation → Customer
    │
    └─ confidence < 0.75 → Escalate
           │
           ▼
    support-service allocate `ticket_no` theo tenant
      từ `support.ticket_counters` bằng `SELECT ... FOR UPDATE`
    support-service tạo ticket (PostgreSQL)
    Đồng thời ghi ops.outbox_events trong CÙNG transaction
           │
           ▼
    OutboxPublisher (background, mỗi 500ms) → RabbitMQ
           │
    ┌──────┴────────────────────┐
    ▼                           ▼                        ▼
notification-service    ai-service (classify)    search-service (index)
Email alert agent       category + priority      ES index ticket
                        sentiment + tags
                               │
                               ▼
                        support-service consumer
                        UPDATE ticket SET category, priority
                               │
                               ▼
                        Agent Dashboard real-time (WebSocket)
                        Ticket mới với AI classification đã điền sẵn
```

### Luồng 2: Agent Xử Lý Ticket

```
Agent mở ticket detail
    │
    ├─ [Manual] Agent gõ reply → POST /tickets/{id}/messages
    │
    └─ [AI Assist] Agent click "Suggest Reply"
           → GET /ai/suggest/{ticketId}
           → ai-service: load full thread + customer memory
           → search similar resolved tickets (ES)
           → generate reply draft
           → cache Redis ai:suggest:{ticketId} TTL 300s
           → hiển thị AISuggestPanel, agent edit → send

Khi reply được gửi:
    - first_response_at update nếu là reply đầu tiên
    - Cache evict: cache:ticket:{tenantId}:{ticketId}
    - search-service re-index async (qua event)
    - notification-service gửi email reply cho customer

Agent click "Resolve":
    - Transition: InProgress → Resolved
    - Set resolved_at, tính SLA met/breached
    - Ghi support.ticket_status_history
    - Ghi ops.outbox_events: support.ticket.resolved.v1
    │
    Fan-out:
    ├─ notification-service → CSAT survey email
    ├─ ai-service → generate resolution summary → update customer_memory
    └─ campaign-service → check follow-up rules → schedule nếu cần
```

### Luồng 3: Knowledge Base Publish & Index

```
Admin viết article trong Admin Portal (TipTap editor)
    │
    ▼
knowledge-service lưu article_version (Markdown + HTML + plain text)
    │
    ▼
Admin click Publish → status = Published
Ghi ops.outbox_events: knowledge.article.published.v1
    │
    Fan-out:
    ├─ search-service: nhận event → index article vào ES (keyword)
    │  + embed content → lưu dense_vector field (semantic)
    ├─ ai-service: update RAG retrieval corpus cho tenant
    └─ portal: gọi Next.js revalidate API → ISR refresh
```

### Luồng 4: Outbox Pattern Chi Tiết

```
ASP.NET Core support-service:
────────────────────────────────────────
BEGIN TRANSACTION;

  INSERT INTO support.tickets (...) VALUES (...);
  
  INSERT INTO ops.outbox_events (
    service_name, aggregate_type, aggregate_id,
    event_type, payload, headers
  ) VALUES (
    'support-service', 'ticket', '{ticketId}',
    'support.ticket.created.v1',
    '{"ticketId":"...","subject":"..."}',
    '{"correlationId":"...", "tenantId":"..."}'
  );

COMMIT;

────────────────────────────────────────
OutboxPublisher (IHostedService, mỗi 500ms):

  -- claim batch an toàn cho nhiều instance
  UPDATE ops.outbox_events
  SET status = 'processing',
      claimed_by = 'support-service:instance-1',
      claimed_at = NOW()
  WHERE id IN (
    SELECT id
    FROM ops.outbox_events
    WHERE status = 'pending'
      AND next_retry_at <= NOW()
    ORDER BY occurred_at ASC
    FOR UPDATE SKIP LOCKED
    LIMIT 100
  )
  RETURNING *;

  FOR EACH event:
    TRY:
      await rabbitMQ.PublishAsync(exchange, routingKey, event);  -- publish confirm bắt buộc
      UPDATE SET status='published', published_at=NOW(), claimed_by=NULL, claimed_at=NULL
    CATCH:
      UPDATE SET
        status='pending',
        retry_count = retry_count + 1,
        next_retry_at = NOW() + backoff_with_jitter(retry_count),
        last_error = error_message,
        claimed_by = NULL,
        claimed_at = NULL
      IF retry_count >= 5:
        UPDATE SET status='dead_letter'  ← alert/manual replay

  -- nếu instance chết giữa chừng, reclaim mọi row processing quá 2 phút

Consumer phía nhận (Inbox Pattern):
  1. Check ops.inbox_messages có event_id này chưa?
  2. Nếu đã xử lý → skip (idempotent)
  3. INSERT INTO ops.inbox_messages (consumer_name, event_id, status='Processing')
  4. Process event
  5. UPDATE status='Done'
```

### Luồng 5: Collision Detection (2 Agents Cùng Reply)

```
Agent A mở ticket → Gateway SET Redis advisory lock:
  SET chat:lock:ticket:{ticketId} {agent_a_id}:{lock_token} NX PX 30000

Agent B mở cùng ticket → GET lock → thấy agent_a_id
  → Frontend hiển thị: "Agent A is currently replying..."
  → Disable reply editor cho Agent B

Agent A heartbeat mỗi 15s:
  → chỉ renew TTL nếu lock_token vẫn match (Lua compare-and-renew)

Agent A submit reply:
  1. support-service UPDATE support.tickets
       SET first_response_at = COALESCE(first_response_at, NOW()),
           row_version = row_version + 1,
           updated_at = NOW()
       WHERE id = {ticketId}
         AND row_version = {expectedVersion};
  2. Nếu affected_rows = 0:
       → trả 409 Conflict + latest ticket snapshot
       → frontend refetch timeline, không overwrite mù
  3. Release advisory lock bằng Lua compare-and-delete theo lock_token
  4. WebSocket broadcast "lock_released" → Agent B có thể reply

Lưu ý:
  - Redis lock chỉ là UX guard, không phải source of truth
  - PostgreSQL row_version mới là concurrency guard cuối cùng
```

### RBAC Permission Matrix

```
Permission                  | Owner | Admin | Manager | Agent | Analyst | Viewer
────────────────────────────────────────────────────────────────────────────────
tickets:create              |   ✓   |   ✓   |    ✓    |   ✓   |    ✗    |   ✗
tickets:view_all            |   ✓   |   ✓   |    ✓    |   ✗   |    ✗    |   ✗
tickets:view_assigned       |   ✓   |   ✓   |    ✓    |   ✓   |    ✗    |   ✗
tickets:assign              |   ✓   |   ✓   |    ✓    |   ✗   |    ✗    |   ✗
tickets:delete              |   ✓   |   ✓   |    ✗    |   ✗   |    ✗    |   ✗
tickets:export              |   ✓   |   ✓   |    ✓    |   ✗   |    ✓    |   ✗
kb:create                   |   ✓   |   ✓   |    ✓    |   ✓   |    ✗    |   ✗
kb:publish                  |   ✓   |   ✓   |    ✓    |   ✗   |    ✗    |   ✗
kb:delete                   |   ✓   |   ✓   |    ✗    |   ✗   |    ✗    |   ✗
campaigns:create            |   ✓   |   ✓   |    ✓    |   ✗   |    ✗    |   ✗
campaigns:send              |   ✓   |   ✓   |    ✗    |   ✗   |    ✗    |   ✗
analytics:view              |   ✓   |   ✓   |    ✓    |   ✗   |    ✓    |   ✓
users:manage                |   ✓   |   ✓   |    ✗    |   ✗   |    ✗    |   ✗
ai_config:manage            |   ✓   |   ✓   |    ✗    |   ✗   |    ✗    |   ✗
tenant:manage               |   ✓   |   ✗   |    ✗    |   ✗   |    ✗    |   ✗
```

---

## 11. Patterns & Implementation Strategy

### 11.1 Clean Architecture (.NET Services)

```
Dependency rule: Infrastructure → Application → Domain
                 API → Application (qua interface)
                 KHÔNG có dependency ngược lại

Domain/
  - Entities: pure C# classes, không có EF attributes
  - Value Objects: immutable (TicketStatus, Priority)
  - Domain Events: raised trong entity methods
  - Repository Interfaces: ITicketRepository (chỉ interface)

Application/
  - Commands/Queries: IRequest<T> (MediatR)
  - Handlers: orchestrate domain + infrastructure calls
  - DTOs: output shapes
  - Validation: FluentValidation pipeline behavior
  - No infrastructure code ở đây

Infrastructure/
  - EF Core DbContext + Migrations
  - Repository implementations
  - RabbitMQ producer (qua OutboxPublisher)
  - Elasticsearch sync
  - External HTTP clients (HttpClientFactory + Polly)

API/
  - Controllers → mediator.Send() → handler
  - No business logic ở controllers
  - Middleware: tenant extraction
```

### 11.2 CQRS (Chỉ Dùng Chọn Lọc)

```
Write side (Commands):
  CreateTicketCommand → CreateTicketHandler:
    1. Validate (FluentValidation pipeline)
    2. Create Ticket domain entity
    3. ticketRepo.Add(ticket)
    4. outboxRepo.Add(TicketCreatedEvent)
    5. unitOfWork.SaveChanges()  ← single transaction
    6. Return TicketDto

Read side (Queries):
  GetTicketListQuery   → Elasticsearch (fast, denormalized, search/filter)
  GetTicketDetailQuery → PostgreSQL (authoritative, join với messages + SLA)
  GetTicketStatsQuery  → PostgreSQL aggregation (hoặc analytics cache)
```

**Nên dùng CQRS cho:** ticket commands/queries, dashboard read model, campaign scheduling

**Không cần CQRS cho:** CRUD đơn giản (feature flags, team management)

### 11.3 Outbox Pattern

Đã mô tả chi tiết trong Luồng 4. Nguyên tắc:
- ghi entity + event trong cùng 1 DB transaction
- OutboxPublisher là background service, không blocking request
- nhiều publisher instance có thể cùng chạy; batch được claim bằng `FOR UPDATE SKIP LOCKED`
- publish phải dùng broker confirm; crash giữa chừng vẫn replay được từ outbox
- retry dùng exponential backoff + jitter qua `next_retry_at`

### 11.4 Inbox Pattern (Idempotency)

Consumer check `ops.inbox_messages` trước khi xử lý event. Đảm bảo at-least-once + idempotent = effectively-once.

Nguyên tắc:
- `(consumer_name, event_id)` là unique key dedupe
- side effects phải safe khi replay (ví dụ email dùng `dedupe_key`, search index upsert theo document id)
- nếu consumer nhận event cũ hơn `aggregate.version` hiện tại của projection thì bỏ qua, không overwrite state mới hơn

### 11.5 Caching Strategy

**Cache-aside cho:**
- ticket detail hot (`cache:ticket:{tenantId}:{ticketId}` TTL 300s)
- KB article hot (`cache:kb:{tenantId}:{slug}` TTL 3600s)
- user permissions (`user_permissions:{userId}:{tenantId}` TTL 3600s)
- dashboard widgets (TTL ngắn, 60–120s)

**Invalidation:**
- ticket updated → evict `cache:ticket:{tenantId}:{ticketId}`
- article published → evict KB cache + trigger ISR
- membership changed → evict permission cache

### 11.6 Ticket Number Allocation Strategy

**Mục tiêu:**
- `ticket_no` là human-readable id cho agent/customer, phải unique trong từng tenant
- internal primary key vẫn là `UUID`; `ticket_no` chỉ là business identifier dễ nói chuyện và tìm kiếm

**Không dùng cách ngây thơ:**
- không dùng `SELECT MAX(ticket_no) + 1` vì vừa full scan vừa race condition
- không dùng global sequence chung cho mọi tenant vì số ticket sẽ lộ volume giữa tenant và tạo global hotspot không cần thiết

**Cách làm v3: tenant-scoped counter row**
- mỗi tenant có 1 row trong `support.ticket_counters`
- khi tạo ticket:
  1. `INSERT INTO support.ticket_counters (tenant_id) VALUES (...) ON CONFLICT DO NOTHING`
  2. `SELECT last_value FROM support.ticket_counters WHERE tenant_id = :tenantId FOR UPDATE`
  3. `UPDATE ... SET last_value = last_value + 1 RETURNING last_value`
  4. format thành `ticket_no = TK-000123`
  5. insert ticket + outbox trong cùng transaction

**Trade-off:**
- row lock chỉ nằm trong phạm vi 1 tenant, nên tenant A không chặn tenant B
- phù hợp với load hiện tại vì burst ticket create/tenant còn thấp
- ưu tiên uniqueness + monotonic per tenant; gapless tuyệt đối không phải mục tiêu cao nhất

**Khi nào cần nâng cấp tiếp:**
- nếu 1 tenant bắt đầu tạo ticket ở mức hàng chục/hàng trăm write/s, `ticket_counters` sẽ thành hot row
- lúc đó nâng cấp sang range allocation: mỗi instance lease block 100 số/lần
- hoặc tách human-readable number thành bước async sau khi ticket UUID đã được commit

### 11.7 Concurrency & Consistency Contract

**Source of truth:**
- PostgreSQL write model là authoritative cho ticket, assignment, status, SLA
- Elasticsearch / Redis / MongoDB là projections hoặc cache, có thể trễ vài giây
- WebSocket/Redis lock chỉ giúp UX, không thay thế validation ở DB

**Optimistic concurrency trên aggregate quan trọng:**
- `support.tickets.row_version` tăng mỗi lần mutate ticket
- mọi command mutate (`assign`, `add-message`, `resolve`, `close`) gửi kèm `expectedVersion`
- write dùng compare-and-swap:
  `UPDATE ... WHERE id = :id AND row_version = :expectedVersion`
- nếu không update được row nào → trả `409 Conflict`, UI refetch và merge lại
- `aggregate.version` trong event envelope luôn mirror committed `row_version`

**Field ownership rõ ràng:**
- agent/manual action sở hữu: `status`, `assigned_agent_id`, `resolved_at`, `internal notes`
- AI chỉ được điền hoặc gợi ý các field derived như `category`, `sentiment`, `tags`, `ai_summary`
- AI không overwrite nếu field đã bị user sửa thủ công hoặc ticket đã sang trạng thái `Resolved/Closed`

**Ordering & stale event rules:**
- hệ thống chỉ cần ordering theo từng aggregate (`ticketId`), không cần global ordering
- projection giữ `last_processed_version`; event cũ hơn version hiện tại bị ignore
- search document nên chứa `aggregate_version` để tránh older event overwrite newer projection

**Eventual consistency chấp nhận được khi nào:**
- search, analytics, notification là side effects; không block command create/resolve ticket
- ticket detail luôn đọc từ PostgreSQL để đảm bảo read-your-writes
- ticket list/search có thể stale tối đa `<= 5s` sau write
- sau khi create ticket, FE chèn optimistic item vào list hoặc redirect thẳng sang detail page theo `ticketId`

### 11.8 Failure Modes & Recovery

**Phân loại lỗi:**
- transient: RabbitMQ unavailable, SMTP timeout, Elasticsearch timeout, OpenAI `429/5xx`
- permanent: payload/schema invalid, aggregate không tồn tại, permission mismatch
- poison message: message parse được nhưng cứ fail lặp lại vì dữ liệu xấu hoặc bug logic

**Retry policy:**
- exponential backoff + jitter, ví dụ: `5s → 15s → 45s → 2m → 5m`
- transient error: reset event về `pending`, set `next_retry_at`
- permanent error: đưa thẳng vào `dead_letter` sớm hơn nếu xác định chắc chắn không recover bằng retry
- sau max retry: move sang `dead_letter`, giữ `last_error`, emit alert

**Recovery / replay runbook:**
1. Tra theo `correlationId` để xem full chain trong logs/traces.
2. Xác định lỗi transient hay permanent.
3. Fix root cause (config, code, credential, data).
4. Replay từ `dead_letter` về `pending` hoặc republish bằng tool nội bộ.
5. Dựa vào Inbox + dedupe keys để replay không gây double side effects.

**Timeouts & circuit breakers:**
- AI call timeout 8–10s; timeout thì fallback sang human handoff, không treo request mãi
- SMTP / search / AI HTTP clients dùng Polly với timeout + circuit breaker + retry có jitter
- notification fail không rollback ticket creation; consistency ở đây là eventual, không transactional

### 11.9 Performance & Scale Envelope

**Load assumptions cho bản CV-ready:**
- 2–5 tenants active cùng lúc, 50–100 concurrent agents
- 2k–5k tickets/ngày, burst 10–20 ticket writes/phút
- realtime chat burst 20–30 msg/s trên hot tenant
- AI workload nhỏ hơn HTTP workload nhưng latency cao hơn: 2–5 RPS tới LLM provider

**Bottleneck có khả năng xảy ra trước:**
1. LLM latency/cost là bottleneck đầu tiên, không phải CPU của API.
2. Elasticsearch hybrid search (BM25 + kNN) là bottleneck thứ hai.
3. Queue backlog ở AI / search indexing nếu một tenant publish nhiều KB articles hoặc burst ticket create.
4. N+1 query ở ticket detail / dashboard nếu không kiểm soát projection và eager loading.

**Scale levers rõ ràng:**
- tách queue theo workload: `notifications`, `search-index`, `ai-tasks`, `campaigns`
- scale ngang `notification-service`, `search-service`, `ai-service` độc lập
- cache ticket detail / KB / permissions để giảm read load lên PostgreSQL
- batch embedding jobs và re-index jobs; không embed đồng bộ trong request path
- rate limit per tenant và per route để một hot tenant không kéo sập cả hệ thống

**Hotspot / consistency trade-off:**
- hot ticket room chỉ broadcast trong room đó, không fan-out tenant-wide
- AI classify và search indexing có thể đến muộn vài giây; UI vẫn dùng PostgreSQL detail làm source of truth
- nếu analytics stream trở thành workload throughput cao, khi đó Kafka mới hợp lý hơn RabbitMQ
- projection freshness (`occurred_at -> indexed_at`) là metric bắt buộc để biết read model có stale quá mức hay không

### 11.10 Service SLOs, Error Budgets & Capacity Assumptions

| Service | SLI / critical path | Target SLO | Error budget / 30d | Capacity assumption | First scale / mitigation move |
|---------|----------------------|------------|--------------------|--------------------|-------------------------------|
| `gateway-bff` | auth'd HTTP requests | 99.9%, p95 < 150ms (không tính AI path) | ~43 phút | 100 req/s burst | scale ngang + cache auth/session |
| `identity-service` | login / refresh token | 99.9%, p95 < 200ms | ~43 phút | 20 auth req/s burst | cache session, tune token queries |
| `workspace-service` | tenant/member CRUD | 99.9%, p95 < 200ms | ~43 phút | 10 writes/s burst | cache permission matrix, read replica nếu cần |
| `support-service` | create/assign/reply/resolve ticket | 99.9%, p95 write < 200ms | ~43 phút | 20 writes/s burst, 200 reads/s | tune indexes, cache hot detail, watch `row_version` conflicts |
| `knowledge-service` | publish/read KB | 99.9%, p95 publish API < 250ms | ~43 phút | 5 publishes/s burst | giữ transform nặng ở async workers |
| `search-service` | search query + projection freshness | 99.5%, p95 query < 300ms, freshness < 5s | ~3h 39m | 50 qps search, 20 index jobs/s | tách worker indexing/search, tune ES mapping |
| `notification-service` | enqueue + delivery | 99.5%, enqueue p95 < 200ms, 95% email < 60s | ~3h 39m | 20 notifications/s burst | split worker theo channel/provider |
| `ai-service` | RAG answer / suggest reply | 99.0%, p95 < 3s, fallback nếu provider down | ~7h 18m | 5 concurrent LLM calls, 20 AI jobs/min/tenant | queue + cache + circuit breaker + degrade gracefully |
| `campaign-service` | scheduled dispatch | 99.0%, fire trong 2 phút quanh lịch hẹn | ~7h 18m | 10k recipients/campaign fan-out batch | batch fan-out, throttle provider, per-campaign advisory lock |

**Error budget policy:**
- nếu một service burn > 25% error budget trong 7 ngày, ưu tiên reliability work thay vì feature mới trên path đó
- nếu `ai-service` burn budget do provider ngoài, degrade sang human handoff thay vì kéo sập support flow
- nếu `search-service` vượt latency budget nhưng write path vẫn khỏe, không rollback feature core; ưu tiên giữ ticket command path ổn định

**Capacity assumptions cần nói rõ khi phỏng vấn:**
- system này không được thiết kế cho “internet scale” ngay từ đầu; nó được tối ưu cho SaaS B2B cỡ nhỏ đến vừa
- scale unit ưu tiên là per-service horizontal scale, không phải jump thẳng sang Kafka/K8s
- chỉ migrate kiến trúc khi đã đo được hotspot thật, không “pre-scale bằng niềm tin”

### 11.11 Observability Bắt Buộc

Mọi service phải emit:
- `correlation_id` và `tenant_id` trong mọi log line
- request duration histogram (p50, p95, p99)
- queue processing duration
- failed job / DLQ counter
- AI latency (riêng)
- search latency (riêng)
- projection freshness (`occurred_at -> indexed_at`)
- version conflict counter (`409 Conflict`) cho optimistic concurrency

---

## 12. AI Integration Strategy

### 12.1 Tổng Quan — AI Có Chỗ Đứng Thật Ở Đâu

| Feature | Trigger | Input | Output |
|---------|---------|-------|--------|
| RAG Chatbot | Customer gửi tin nhắn | Query + KB articles + customer memory | Answer + citations |
| Auto-classify | `support.ticket.created.v1` | Subject + description | category, priority, sentiment, tags |
| Agent Assist | Agent request suggest | Full ticket thread + customer memory | Reply draft |
| Semantic Search | User search query | Query text | Ranked articles + tickets |
| Ticket Summary | Ticket assigned/resolved | Full message thread | Summary text |
| Memory Update | `support.ticket.resolved.v1` | Resolution summary | Updated customer profile |

### 12.2 RAG Pipeline Chi Tiết

```
Customer query: "Làm sao reset mật khẩu trên mobile?"
    │
Step 1 — EMBED QUERY
  openai.embeddings.create({ model: "text-embedding-3-small", input: query })
  → vector [1536 dims]
    │
Step 2 — RETRIEVE (Elasticsearch hybrid)
  POST /kb_articles_v1/_search
  {
    "knn": {
      "field": "embedding",
      "query_vector": [...],
      "k": 5, "num_candidates": 50,
      "filter": { "term": { "tenant_id": "uuid" } }  // tenant isolation bắt buộc
    },
    "query": {
      "bool": { "must": { "match": { "body_plain": "reset mật khẩu" } } }
    }
  }
  → top 3 chunks (hybrid: semantic + keyword BM25)
    │
Step 3 — AUGMENT CONTEXT
  customer_memory = MongoDB.findOne({ tenant_id, customer_email })
  context = { history_summary, retrieved_articles: [{title, body_plain, url}] }
    │
Step 4 — GENERATE
  system_prompt = "You are support assistant for {tenant_name}.
    Answer ONLY based on the provided KB articles.
    If unsure, say so and offer to connect to human agent.
    Always cite your sources with article URLs."
    
  response = await openai.chat.completions.create({
    model: "gpt-4o-mini",
    messages: [system, { role: "user", content: "Context:\n{articles}\n\nQuestion: {query}" }],
    temperature: 0.3,   // thấp để grounded
    max_tokens: 500
  })
    │
Step 5 — EVALUATE & RESPOND
  confidence = calculateConfidence(response, retrievedArticles)
  IF confidence >= 0.75:
    → emit AIResponseReady → Chat Service → WebSocket → Customer
  ELSE:
    → emit EscalateToAgent → Create ticket → Alert agent
    │
Step 6 — STORE & LEARN
  MongoDB: append to chat_session.messages + citation sources
  Redis: update short-term context cache (TTL 30min)
  Async: schedule job to update customer_memory summary
```

### 12.3 Ollama Self-Host (Dev / Fallback)

```bash
# docker-compose.yml
ollama:
  image: ollama/ollama
  ports: ["11434:11434"]
  volumes: ["ollama_data:/root/.ollama"]

# Pull models (chạy 1 lần)
docker exec ollama ollama pull llama3.2:3b      # 2GB — chat/assist
docker exec ollama ollama pull nomic-embed-text  # 274MB — embeddings

# ai-service env
LLM_PROVIDER=openai          # openai | ollama
LLM_MODEL=gpt-4o-mini
LLM_BASE_URL=https://api.openai.com
FALLBACK_TO_OLLAMA=true
OPENAI_API_KEY=sk-...
```

### 12.4 AI Safety Rules

- không trả lời nếu retrieval yếu (không đủ KB context)
- luôn filter retrieval theo `tenant_id` — không bao giờ cross-tenant
- luôn cố citation nếu trả lời từ KB
- mọi AI outputs phải log vào MongoDB `ai_runs`
- AI suggestion không được tự động commit business action nhạy cảm
- `confidence < 0.75` → escalate, không hallucinate

### 12.5 Rule-Based Thay LLM Khi Có Thể

```
Dùng rule thay LLM:
  - SLA breach detection: cron job so sánh sla_deadline vs NOW()
  - Auto-assign: round-robin trong team (COUNT query per agent)
  - Rate limiting: Redis counter
  - Duplicate ticket: title similarity hash check
  - Business hours: timezone config

Dùng ES aggregation thay LLM:
  - Top issue categories: ES aggregation by category
  - Agent workload: COUNT tickets per agent
  - KB article recommendation: more_like_this query
```

---

## 13. API Strategy

### 13.1 Gateway Endpoints Tiêu Biểu

```
# Auth
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me

# Workspace
GET  /api/workspace/me          ← tenant context + role
POST /api/workspace/invite
GET  /api/workspace/members

# Customers
GET  /api/customers
POST /api/customers
GET  /api/customers/{id}

# Tickets
GET  /api/tickets               ← list với filters + pagination
POST /api/tickets
GET  /api/tickets/{id}
POST /api/tickets/{id}/messages
POST /api/tickets/{id}/assign
POST /api/tickets/{id}/resolve
GET  /api/tickets/{id}/history  ← status history

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
- `POST /api/tickets`, `POST /api/tickets/{id}/messages`, `POST /api/campaigns/{id}/schedule` nên hỗ trợ header `Idempotency-Key`
- command mutate ticket nên gửi kèm `expectedVersion` để backend enforce optimistic concurrency

### 13.2 FE Data Fetching Rules

- list pages: pagination + filters + TanStack Query
- detail pages: `staleTime: 5 * 60 * 1000`, `staleWhileRevalidate`
- mutations: optimistic update chỉ ở nơi ít rủi ro (status update, add message)
- dashboard widgets: `staleTime: 60 * 1000` (1 phút)
- AI suggest: manual trigger (không auto-fetch)

---

## 14. Roadmap 10 Tuần

> **Nguyên tắc:** Mỗi cuối tuần phải có deliverable chạy được thật. Không skip foundation.

---

### Tuần 1: Foundation & Infrastructure

**Mục tiêu:** 2 repo chạy, local infra up, health checks xanh.

```
✓ Tạo signaldesk-fe (Turborepo) + signaldesk-be (NX)
✓ docker-compose.infra.yml:
  PostgreSQL, MongoDB, Redis, RabbitMQ, Elasticsearch, MinIO, Mailpit, Ollama
✓ RabbitMQ: tạo exchanges + queues + bindings + DLQ
✓ PostgreSQL: tạo schemas (identity, workspace, support, knowledge, notification, ops)
✓ Shared libs bootstrap (BuildingBlocks .NET, common NestJS)
✓ Structured logging: Serilog (.NET) + Winston (NestJS) với correlationId
✓ Health check endpoints mọi service
✓ GitHub Actions CI: lint + build on PR
```

**Deliverable:** `docker-compose up` → tất cả services và infra xanh.

---

### Tuần 2: Identity & Workspace (Auth + RBAC)

**Mục tiêu:** Login, register tenant, RBAC cơ bản.

```
✓ identity-service:
  - POST /auth/register → tạo user
  - POST /auth/login → JWT (15min) + refresh token (7d, httpOnly cookie)
  - POST /auth/refresh
  - GET  /auth/me
✓ workspace-service:
  - POST /workspace → tạo tenant + gán Owner
  - POST /workspace/invite → gửi invite email
  - GET  /workspace/members
  - RBAC: seed default roles + permissions
  - Publish workspace.tenant.created.v1 → ops.outbox_events
✓ notification-service: consume workspace.tenant.created.v1 → gửi welcome email
✓ gateway-bff:
  - JWT middleware: verify → extract userId + tenantId → forward headers
  - Rate limiting: Redis token bucket
  - Correlation ID middleware
✓ Next.js workspace app: login page + protected layout + route guards
```

**Deliverable:** Đăng ký tenant → login → nhận JWT → access protected endpoint.

---

### Tuần 3: Support Core (Ticket Lifecycle)

**Mục tiêu:** Ticket lifecycle đầy đủ.

```
✓ support-service (Clean Architecture + CQRS + MediatR):
  - Support schema migrations: customers, tickets, ticket_messages,
    ticket_status_history, sla_policies, ticket_counters, teams
  - Commands: CreateTicket, AssignTicket, AddMessage, ResolveTicket, CloseTicket
  - Queries: GetTickets (list + filter), GetTicketById (detail + messages)
  - Tenant-scoped `ticket_no` allocation bằng `ticket_counters` + `FOR UPDATE`
  - OutboxPublisher: support.ticket.created/assigned/resolved → RabbitMQ
  - SLA deadline calculation khi tạo ticket
  - Audit log: mọi status change ghi ops.audit_logs
✓ notification-service consumers:
  - ticket.created → email agent assigned
  - ticket.resolved → email closure + CSAT survey to customer
✓ Next.js workspace:
  - Ticket list page (SSR, filter by status/priority/agent)
  - Ticket detail page: message timeline + reply editor
  - TanStack Query: optimistic update khi reply
  - Zustand: auth + permissions store
```

**Deliverable:** Agent nhận ticket, reply, resolve → customer nhận email. Audit trail đầy đủ.

---

### Tuần 4: Knowledge Base + Public Portal

**Mục tiêu:** KB có thể publish, portal SSR/ISR hoạt động.

```
✓ knowledge-service (Clean Architecture):
  - Migrations: categories, articles, article_versions, files
  - Commands: CreateArticle, UpdateArticle, PublishArticle, UnpublishArticle
  - Versioning: mỗi lần save tạo article_version mới
  - File upload: presigned URL → MinIO
  - Publish: status = Published + ghi outbox
✓ Next.js workspace (Admin):
  - KB management pages: article list, TipTap WYSIWYG editor
  - Category tree management
✓ Next.js portal:
  - Home page (SSG)
  - /help/[slug] article page (ISR, revalidate: 3600)
  - On-demand revalidate khi article published
  - Public search page
```

**Deliverable:** Admin publish article → portal hiển thị ngay (ISR). SEO-ready.

---

### Tuần 5: RabbitMQ + Outbox + Elasticsearch Search

**Mục tiêu:** Event-driven flow hoàn chỉnh + full-text search.

```
✓ Outbox Pattern hoàn chỉnh trong support-service và knowledge-service:
  - OutboxPublisher (IHostedService, poll 500ms)
  - Batch claim events bằng `FOR UPDATE SKIP LOCKED`
  - Retry logic: exponential backoff + jitter, max 5 lần, sau đó dead_letter
  - Manual replay runbook cho dead_letter
✓ Inbox Pattern trong consumers (deduplication qua ops.inbox_messages)
✓ search-service (NestJS):
  - Elasticsearch client setup
  - Consumer: knowledge.article.published.v1 → index kb_articles_v1
  - Consumer: support.ticket.created.v1 → index tickets_v1
  - GET /search?q=...&type=all → unified search endpoint
  - Filter bắt buộc theo tenant_id
✓ workspace app: search bar với kết quả KB + ticket
✓ Analytics cơ bản:
  - workspace-service: GET /analytics/overview (ticket counts + SLA stats)
  - Grafana dashboard seed cơ bản
```

**Deliverable:** RabbitMQ events flow đầy đủ. Search tickets và KB trong cùng UI.

---

### Tuần 6: Real-time Chat Widget

**Mục tiêu:** Live chat widget nhúng được, agent và customer communicate real-time.

```
✓ gateway-bff: WebSocket endpoint, Socket.io với Redis adapter
✓ Chat sessions lưu MongoDB (chat_db)
✓ Presence tracking: Redis key TTL 30s + heartbeat refresh
✓ Collision lock: Redis advisory lock per ticket (NX + TTL + lock token)
✓ Events: join_room, send_message, typing_indicator, presence_update
✓ widget-sdk (Next.js/iframe):
  - Embeddable JS snippet (<script> loader)
  - Chat widget UI
  - Socket.io client
✓ workspace app: chat view cho agent
```

**Deliverable:** Customer chat với agent real-time. Typing indicator. Collision detection.

---

### Tuần 7: AI v1 — RAG + Classify + Agent Assist

**Mục tiêu:** RAG chatbot grounded, agent assist, auto-classify.

```
✓ ai-service setup:
  - RabbitMQ consumers
  - LLM factory (OpenAI + Ollama fallback theo env)
✓ Embedding pipeline:
  - Consumer: knowledge.article.published.v1 → embed body_plain → store ES dense_vector
  - Batch re-embed endpoint cho admin
✓ RAG Chatbot:
  - kNN hybrid search trong ES (semantic + BM25)
  - Build grounded prompt với customer_memory
  - confidence threshold: < 0.75 → escalate
  - Cite sources trong response
✓ Auto-classify:
  - Consumer: support.ticket.created.v1
  - category + priority + sentiment → emit ai.classification.completed.v1
  - support-service consumer update ticket
✓ Agent Assist:
  - GET /ai/tickets/{id}/suggest-reply
  - Cache Redis 300s
  - AISuggestPanel component trong ticket detail
✓ Customer memory:
  - Consumer: support.ticket.resolved.v1 → generate summary → update MongoDB
  - RAG pipeline load memory trước khi generate
✓ AI run logs: MongoDB ai_runs
```

**Deliverable:** Bot trả lời grounded có citation. Agent thấy AI suggest. Ticket mới tự có category/priority.

---

### Tuần 8: Caching, Rate Limiting, Performance

**Mục tiêu:** System ổn định ở load thực tế.

```
✓ Redis cache-aside hoàn chỉnh:
  - ticket detail, KB article, user permissions
  - Cache invalidation on update
✓ Rate limiting tại gateway:
  - Per tenant + per endpoint
  - Redis token bucket
✓ Redis Pub/Sub cho WebSocket multi-instance (nếu chưa có ở tuần 6)
✓ Semantic search trong search-service: hybrid BM25 + kNN
✓ k6 load test đầu tiên:
  - 50 VU, 3 phút
  - Identify bottlenecks
  - Fix top 3 slow queries (EXPLAIN ANALYZE)
✓ PostgreSQL indexes review + thêm nếu thiếu
```

**Deliverable:** System không bị chậm với concurrent users. Cache hit rate > 75%.

---

### Tuần 9: Campaign Automation (Optional) + Polish

**Mục tiêu:** Admin tạo campaign, follow-up tự động. Polish UI.

```
✓ campaign-service (nếu có thời gian):
  - CampaignDefinition CRUD
  - Segment builder: filter customers theo criteria
  - Hangfire scheduled job: execute campaign → fan-out email per recipient
  - Distributed lock: tránh double-send
  - Track send results
✓ Post-resolution follow-up:
  - Consumer: support.ticket.resolved.v1 → schedule follow-up notification
✓ UI polish:
  - Lighthouse score ≥ 90 trên portal
  - Web Vitals: LCP < 2.5s
  - Code splitting cho heavy components
  - Error states + empty states
✓ Seed demo data:
  - Script tạo: 2 tenants, 10 agents, 100+ tickets, 30+ KB articles
  - Demo accounts: admin@taskflow.io / agent@taskflow.io / admin@invoicefox.io
```

**Deliverable:** Ticket resolve → follow-up email tự động. Demo data realistic.

---

### Tuần 10: Deploy + Observability + Portfolio

**Mục tiêu:** Production live. Monitoring hoạt động. Portfolio sẵn sàng.

```
✓ Docker Compose production:
  - Resource limits, health checks, named volumes/networks
✓ Nginx: reverse proxy, SSL (Let's Encrypt + Certbot), gzip, caching headers
✓ GitHub Actions pipelines:
  - ci.yml: PR → lint + test + build
  - deploy-backend.yml: merge → Docker build → push → SSH deploy
  - deploy-frontend.yml: merge → build → deploy
✓ OpenTelemetry SDK:
  - .NET: Microsoft.Extensions.Telemetry + OTel exporter → Jaeger/Tempo
  - NestJS: @opentelemetry/sdk-node + exporter
  - Trace propagation qua HTTP headers (correlationId)
✓ Prometheus metrics:
  - ASP.NET: prometheus-net
  - NestJS: prom-client
  - Scrape config
✓ Grafana dashboards:
  - API latency per service (p50, p95, p99)
  - RabbitMQ queue depth + oldest ready message age
  - Redis hit rate
  - Error rate per service
  - AI latency
  - SLO burn rate alerts cho gateway/support/search/ai
✓ Loki + Promtail: Docker log driver → Promtail → Loki → Grafana log explorer
✓ Sentry (FE + BE)
✓ UptimeRobot (30 ngày)
✓ k6 load test final:
  - 100 VU, 5 phút, mixed scenario
  - Document: p95 latency, error rate, throughput
✓ Demo video (Loom, 5–7 phút) — xem Demo Scenarios ở section 17
✓ README.md: overview, quick start, architecture diagram, tech stack
```

**Deliverable:** `production.yourdomain.com` chạy thật. Grafana dashboard live. k6 results documented. README professional.

---

### Tuần 11–12: Nếu Nhanh (Stretch Goals)

```
Tuần 11:
  - Migrate từ RabbitMQ sang Kafka cho analytics stream
    (lúc này đã có lý do: throughput analytics cần ordered events)
  - Kafka UI: Kafka consumer lag monitoring
  - Dashboard metrics chi tiết hơn
  - AI evaluation set: tạo test cases để measure RAG quality

Tuần 12:
  - K8s demo (Minikube hoặc cheap cluster)
  - Advanced customer memory hoặc campaign scoring
  - Tách workspace thành admin app và agent app riêng nếu muốn
```

---

## 15. Definition of Done — CV-Ready Checklist

Project được xem là đủ mạnh để đưa vào CV khi có đủ các mục sau:

```
Infrastructure & Deploy:
  ✓ 1 URL demo chạy thật (không phải localhost)
  ✓ SSL/HTTPS
  ✓ CI/CD pipeline chạy được (GitHub Actions)
  ✓ UptimeRobot chạy ≥ 30 ngày → có screenshot uptime

Core Features:
  ✓ Auth + RBAC (ít nhất 2 roles hoạt động)
  ✓ Ticket lifecycle end-to-end (create → assign → reply → resolve)
  ✓ KB publish + public portal
  ✓ Search hoạt động (keyword ít nhất)
  ✓ RabbitMQ + Outbox Pattern (có thể demo event flow)
  ✓ AI: ticket summary + RAG answer có citation
  ✓ ít nhất 2 tenants dữ liệu giả lập rõ ràng

Observability:
  ✓ 1 Grafana dashboard live (latency + error rate + throughput)
  ✓ Distributed traces hoạt động (Jaeger/Tempo)
  ✓ k6 load test results documented

Portfolio:
  ✓ README professional (overview, setup, architecture diagram)
  ✓ Demo video 3–5 phút
  ✓ GitHub repo với badges (build status, uptime)
```

---

## 16. Metrics Cần Đo

| Metric | Target | Tool |
|--------|--------|------|
| API p95 latency | < 200ms | Grafana (k6 + Prometheus) |
| API p99 latency | < 500ms | Grafana |
| Throughput | 200+ req/min | k6 |
| Cache hit rate | > 75% | Redis INFO + Grafana |
| RabbitMQ ready queue depth | < 100 steady-state | RabbitMQ UI + Prometheus |
| Oldest ready message age | < 5s | RabbitMQ UI + Prometheus |
| ES search latency | < 80ms | ES slow log |
| Projection freshness (PG -> ES) | < 5s | Custom metric: `occurred_at -> indexed_at` |
| AI response time | < 3s | Custom metric (ai_runs) |
| AI confidence avg | > 0.80 | MongoDB ai_runs aggregation |
| AI suggestion acceptance rate | track (no target) | MongoDB prompt_feedback |
| Version conflict rate (`409`) | track (alert nếu tăng đột biến) | API metrics |
| Ticket number allocation lock wait | < 50ms p95 | PostgreSQL lock metrics / custom |
| DLQ count | 0 (alert nếu > 0) | RabbitMQ UI |
| SLO burn rate | alert nếu > 2x ngân sách theo 1h/6h windows | Grafana |
| CI/CD pipeline time | < 5 phút | GitHub Actions |
| Uptime | > 99.5% (30 ngày) | UptimeRobot |
| Lighthouse score (portal) | ≥ 90 | Lighthouse CI |
| LCP (portal) | < 2.5s | Web Vitals |

---

## 17. Portfolio & Interview Prep

### 5 Điểm WOW Khi Phỏng Vấn

**WOW #1: Multi-tenant SaaS với data isolation**
> "Mọi query đều mandatory filter theo `tenant_id`. Không có cơ chế nào cho phép data leak giữa tenant — `tenantId` extract từ JWT tại Gateway và inject vào mọi downstream request header. Redis keys, ES queries, AI retrieval — tất cả đều prefixed/filtered theo tenant."

**WOW #2: Outbox + Inbox Pattern — zero message loss**
> "Ticket creation và event publish nằm trong cùng 1 DB transaction. Nếu RabbitMQ down 30 phút, khi recover, OutboxPublisher tự publish lại toàn bộ missed events. Consumer có Inbox deduplication để không process event 2 lần dù broker deliver nhiều lần."

**WOW #3: RAG với confidence-based escalation**
> "Bot không chỉ call LLM random — nó retrieve KB articles của đúng tenant bằng hybrid search (BM25 + kNN), build grounded context với customer long-term memory, và nếu confidence < 0.75 thì tự escalate sang agent thay vì hallucinate. Mọi AI inference được log để track quality."

**WOW #4: Distributed tracing end-to-end**
> "Bất kỳ request nào cũng có `correlationId` propagate qua Gateway → support-service → RabbitMQ → ai-service → notification-service. Có thể replay toàn bộ lifecycle của 1 ticket trên Jaeger/Tempo trong 30 giây."

**WOW #5: Production với CI/CD + monitoring**
> "GitHub Actions build → test → Docker build → SSH deploy trong < 5 phút. Grafana alert nếu p95 latency vượt 300ms. UptimeRobot verify 99.5% uptime sau 30 ngày."

---

### Demo Scenarios

**Scenario A — "RAG Chatbot không hallucinate" (3 phút)**
```
1. Mở customer chat widget trên website TaskFlow
2. Hỏi câu có answer trong KB: "How do I reset my password on mobile?"
3. Show bot response với source citation (link article)
4. Hỏi câu ngoài KB: "What's the refund policy for annual plans?"
5. Bot nói: "I'm not confident about this — connecting you to an agent"
6. Show Jaeger trace: widget → gateway → chat-session → ai-service → ES → LLM
7. Show MongoDB ai_runs: confidence_score = 0.52, escalated = true
```

**Scenario B — "Event-driven pipeline sống" (2 phút)**
```
1. Tạo ticket mới từ workspace
2. Show RabbitMQ UI: message xuất hiện trong queue
3. Show Mailpit: email notification đến agent
4. Show ticket trong UI: đã có category/priority từ AI classify
5. Show Grafana: ready messages = 0, oldest message age < 1s
```

**Scenario C — "Tenant isolation" (1 phút)**
```
1. Login với admin@taskflow.io → thấy tickets của TaskFlow
2. Login với admin@invoicefox.io → thấy tickets của InvoiceFox
3. Thử call API với JWT của taskflow, pass tenantId của invoicefox
4. Gateway reject: "Tenant mismatch — X-Tenant-Id doesn't match JWT claim"
```

**Scenario D — "Observability stack" (2 phút)**
```
1. Show Grafana dashboard: latency p50/p95/p99, throughput, error rate
2. Show Jaeger: distributed trace của 1 request phức tạp (ticket create → notify → classify)
3. Show Loki: log search "support.ticket.created"
4. Show k6 report: p95 < 200ms tại 100 VUs
```

---

### CV Bullet Points

```
• Architected a multi-tenant SaaS customer support platform using microservices
  (ASP.NET Core + NestJS), featuring event-driven communication via RabbitMQ
  with Outbox + Inbox Pattern ensuring zero message loss and idempotent processing
  across 8 independent services; achieved p95 API latency < 200ms at 200 req/min
  under k6 load testing.

• Engineered a production-grade RAG pipeline integrating LangChain.js, OpenAI
  text-embedding-3-small, and Elasticsearch hybrid search (BM25 + kNN) to power
  an AI support chatbot with confidence-based escalation and long-term customer
  memory; handled 60%+ of customer queries without agent intervention in load tests.

• Delivered full observability and CI/CD stack — distributed tracing via Jaeger
  (OpenTelemetry), metrics/alerts in Prometheus + Grafana, log aggregation in Loki —
  deployed on VPS via GitHub Actions with Docker Compose, maintaining 99.5% uptime
  over 30-day production run.
```

---

### Các Câu Hỏi Phỏng Vấn & Câu Trả Lời Prepared

**Q: Tại sao dùng RabbitMQ thay Kafka?**
A: RabbitMQ phù hợp với workload hiện tại — workflow-based messaging, business events, dễ vận hành solo. Kafka phù hợp hơn khi cần ordered stream, log compaction, hoặc consumer group lag tracking cho analytics. Nếu analytics stream trở thành bottleneck, tôi sẽ migrate ai-service và analytics sang Kafka — đó là stretch goal ở tuần 11.

**Q: Outbox Pattern hoạt động thế nào?**
A: Ghi domain entity và event vào cùng 1 DB transaction. Publisher claim batch bằng `FOR UPDATE SKIP LOCKED`, publish với broker confirm, rồi mới mark `published`. Nếu app crash sau khi commit DB nhưng trước khi publish, row outbox vẫn còn và sẽ được replay. Kết hợp với Inbox Pattern ở consumer để đảm bảo idempotent.

**Q: Làm sao đảm bảo tenant isolation?**
A: Ba lớp — DB (tenant_id column bắt buộc trên mọi table + application-level filter), Cache (Redis keys prefix bằng tenantId), AI/Search (filter bắt buộc trong mọi ES query, RAG chỉ retrieve của đúng tenant). tenantId được extract từ JWT tại Gateway và forward qua header — không tin vào bất kỳ tenantId nào client truyền lên.

**Q: RAG pipeline của bạn handle hallucination thế nào?**
A: System prompt chỉ cho phép trả lời dựa trên KB articles được cung cấp. LLM temperature thấp (0.3) để grounded. Confidence scoring dựa trên retrieval quality + response grounding — nếu < 0.75 thì escalate, không bịa. Citation bắt buộc trong response.

**Q: CQRS trong project có thực sự cần thiết không?**
A: Tôi dùng CQRS chọn lọc, không áp dụng toàn bộ. Ticket list query đọc từ Elasticsearch (fast, denormalized, filter), ticket detail đọc từ PostgreSQL (authoritative). Command và query handlers riêng qua MediatR. Phần CRUD đơn giản như feature flags không cần CQRS.

**Q: Tại sao eventual consistency ở đây là chấp nhận được?**
A: Vì search, analytics, notification là side effects chứ không quyết định transactional correctness của ticket. `create/assign/resolve` commit vào PostgreSQL trước, còn Elasticsearch và notification có thể đến sau vài giây. Để giữ UX tốt, detail page luôn đọc PostgreSQL và FE đảm bảo read-your-writes bằng optimistic insert hoặc redirect sang detail.

**Q: Làm sao tránh 2 agent hoặc AI overwrite state của nhau?**
A: Ticket có `row_version` và mọi command mutate đều gửi `expectedVersion`. Backend update theo compare-and-swap; nếu version không khớp thì trả `409 Conflict`. Redis lock chỉ là UX guard, còn concurrency guard cuối cùng là PostgreSQL. AI cũng chỉ được điền field derived, không overwrite field manual như status hay assignment.

**Q: Redis lock của bạn safe thế nào?**
A: Tôi không dùng `SETNX` rồi `DEL` đơn giản. Lock value phải chứa `lock_token`, renew và release bằng Lua compare-and-renew / compare-and-delete để owner cũ không xóa lock mới của owner khác sau khi TTL hết hạn.

**Q: Nếu message bị lỗi lặp lại thì xử lý sao?**
A: Tôi phân biệt transient với permanent failure. Transient thì retry bằng exponential backoff + jitter; permanent hoặc poison message thì đưa vào `dead_letter` sớm hơn. Mỗi dead-letter giữ `last_error`, trace theo `correlationId`, và có manual replay runbook sau khi fix root cause.

**Q: System này sẽ nghẽn ở đâu đầu tiên khi scale lên?**
A: Đầu tiên thường là AI latency/cost, sau đó là Elasticsearch hybrid search, rồi đến queue backlog của AI/indexing trên hot tenant. Vì vậy tôi tách queue theo workload, scale ngang search/AI riêng, cache dữ liệu hot, và chỉ chuyển Kafka vào analytics stream khi throughput/order thực sự cần.

**Q: Làm sao replay DLQ mà không gửi email 2 lần hoặc index sai?**
A: Consumer phải idempotent. Email dùng `dedupe_key`, search dùng upsert theo document id + `aggregate_version`, còn consumer nào xử lý rồi thì Inbox Pattern chặn xử lý lặp lại theo `(consumer_name, event_id)`. Vì vậy replay là safe sau khi root cause được sửa.

**Q: Tại sao không dùng `MAX(ticket_no) + 1` để cấp số ticket?**
A: Vì cách đó vừa race condition vừa tệ về performance do phải scan/index lookup liên tục. Tôi dùng `support.ticket_counters` với row lock `FOR UPDATE` theo từng tenant, nên uniqueness được đảm bảo trong transaction mà tenant này không chặn tenant khác. Nếu sau này một tenant quá nóng, tôi sẽ chuyển sang range allocation để tránh hot row.

**Q: Bạn dùng SLO và error budget vào quyết định kỹ thuật thế nào?**
A: Tôi không coi SLO là dashboard cho đẹp. Nó là contract để biết service nào được phép degrade mà không ảnh hưởng path chính. Ví dụ `ai-service` có SLO thấp hơn `support-service`, nên khi provider AI lỗi tôi degrade sang human handoff thay vì làm ticket flow fail. Nếu burn rate vượt ngưỡng, tôi ưu tiên hardening và capacity tuning trước khi thêm feature mới.

---

## 18. Cut-Scope Guide Khi Bị Chậm

Nếu tiến độ chậm, cắt theo thứ tự sau (cắt trên trước):

1. `campaign-service` (tuần 9)
2. Kafka migration (tuần 11–12)
3. True micro-frontend (widget-sdk riêng repo)
4. K8s
5. Advanced customer memory
6. Collision detection WebSocket (giữ WebSocket, bỏ lock)

**Không được cắt nếu muốn project vẫn mạnh:**
- tenant + RBAC
- ticket lifecycle
- KB + search (keyword search tối thiểu)
- RabbitMQ + Outbox Pattern
- AI summary + RAG answer có citation
- deploy thật (URL live)
- observability cơ bản (1 Grafana dashboard)

**Fallback nếu bị trễ rất nhiều:**
Merge `identity-service` và `workspace-service` thành 1 service `auth-service`. Đây là compromise hợp lý và không ảnh hưởng đến điểm mạnh của project.

---

## 19. Context Handoff

### Khi Mang File Này Sang Chat Window Mới

Paste toàn bộ file này vào context và dùng prompt:

> Đây là tài liệu source of truth của project SignalDesk AI. Hãy dùng nó làm base và giúp tôi triển khai: **[ghi rõ task cụ thể]**.
>
> Context hiện tại: Tôi đang ở **Tuần [N]**, đã hoàn thành [liệt kê deliverables], đang implement [feature đang làm].

### Khi Bắt Đầu Một Task Cụ Thể

Ví dụ:

> "Tôi đang implement OutboxPublisher cho support-service. Project dùng ASP.NET Core 8, EF Core 8, RabbitMQ. Schema outbox là ops.outbox_events với các fields: id, service_name, aggregate_type, aggregate_id, event_type, payload, headers, status, retry_count, next_retry_at, claimed_by, claimed_at, last_error, occurred_at, published_at. Hãy implement IHostedService poll mỗi 500ms với batch claim bằng `FOR UPDATE SKIP LOCKED`, broker confirm, và exponential backoff + jitter."

### Trạng Thái Hiện Tại (Cập Nhật Khi Bắt Đầu Phase Mới)

```
Phiên bản tài liệu: v3.0
Cập nhật lần cuối:  [ngày bắt đầu]
Tuần hiện tại:      [ ]
Deliverables hoàn thành:
  - [ ]
Đang implement:
  - [ ]
Quyết định đã chốt:
  - Message broker: RabbitMQ (Kafka ở tuần 11-12 nếu có thời gian)
  - FE: portal + workspace + widget-sdk (Turborepo)
  - BE: identity + workspace + support + knowledge + notification + search + ai (NX)
  - PostgreSQL multi-schema (single instance)
```

---

*Tài liệu này được duy trì bởi owner. Cập nhật `Trạng Thái Hiện Tại` khi bắt đầu mỗi tuần mới.*

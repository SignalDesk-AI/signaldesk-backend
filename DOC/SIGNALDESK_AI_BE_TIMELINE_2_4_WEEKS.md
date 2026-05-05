# SignalDesk AI - Backend Timeline 2-4 Weeks

> Timeline thực thi riêng cho Backend, tổng hợp từ `SIGNALDESK_AI.md` và `SIGNALDESK_AI_BE_v6_0.md`.
> FE sẽ chạy song song ở agent khác, nên timeline này ưu tiên API contract, event contract, môi trường deploy và các điểm handoff rõ ràng.
> File này chỉ nói "làm gì, tích hợp tech stack khi nào", không chứa code mẫu hoặc hướng dẫn implement chi tiết.

## 1. Cách Đọc Timeline

Timeline chính là 4 tuần, tương đương 20 ngày làm việc. Không có hướng cắt scope trong file này: nếu phần nào vượt khỏi 4 tuần thì kéo dài thời gian và tiếp tục đúng thứ tự, không thay bằng bản rút gọn.

Nguyên tắc triển khai:

- Backend đi theo thứ tự: source foundation -> identity/workspace/gateway -> support core -> event pipeline -> knowledge/search -> AI -> production hardening.
- Không đợi FE xong mới làm BE. Mỗi tuần phải có API contract hoặc mockable endpoint để FE agent bám vào.
- Không đưa business logic vào `gateway-bff`. Gateway chỉ xử lý auth propagation, tenant resolution, rate limit, correlation ID, proxy và WebSocket entrypoint.
- PostgreSQL là source of truth cho transactional data. Elasticsearch, Redis, MongoDB là projection, cache hoặc AI/context store.
- Mọi publish domain event từ .NET service phải đi qua Outbox Pattern.
- Mọi async consumer phải có deduplication:
  - .NET services dùng `ops.inbox_messages` nếu có PostgreSQL.
  - `notification-service` và `search-service` dùng Redis inbox key.
  - `ai-service` dùng MongoDB `inbox_messages`.
- Các phần nâng cao như `campaign-service`, webhook system, Qdrant/pgvector, Kafka analytics, Kubernetes demo, advanced OIDC và Python ML service vẫn được giữ trong backlog đầy đủ. Thứ tự triển khai là core trước, expansion sau, không xóa khỏi scope.

## 2. Scope Chốt Cho Full Backend

### Core Platform

- `signaldesk-be` monorepo.
- Local infrastructure bằng Docker Compose: PostgreSQL, PgBouncer, MongoDB, Redis, RabbitMQ, Elasticsearch, MinIO, Mailpit, Ollama.
- `gateway-bff` bằng NestJS.
- `identity-service`, `workspace-service`, `support-service`, `knowledge-service` bằng ASP.NET Core 8.
- `notification-service`, `search-service`, `ai-service` bằng NestJS.
- `campaign-service` bằng ASP.NET Core 8.
- Multi-tenant auth, RBAC, feature flags cơ bản.
- Ticket lifecycle: create, assign, add message, internal note, resolve, close, soft delete.
- Knowledge base: article CRUD, versioning, publish/unpublish, attachment metadata.
- RabbitMQ + Outbox + Inbox + DLQ.
- Search: ticket/article indexing, keyword search, semantic search, hybrid retrieval.
- AI v1: classify ticket, summarize ticket, RAG answer có citation và confidence threshold.
- Redis cache/rate limit/presence/idempotency support.
- WebSocket live ticket update, typing indicator, presence, collision detection.
- Campaign follow-up sau ticket resolved.
- k6 load test và tối ưu top slow queries.
- Seed demo data: 2 tenants, 10 agents, 100+ tickets, 30+ KB articles.
- Observability đầy đủ: structured logs, health checks, Prometheus metrics, OpenTelemetry traces, Grafana dashboards, Loki logs, SLO alerts.
- Production deploy: Docker Compose production, Nginx, SSL, GitHub Actions, VPS, smoke test, UptimeRobot.

### Expansion Sau Khi Core Production Ổn

- OIDC-compatible auth endpoints nếu muốn trình bày enterprise identity sâu hơn.
- ABAC policy layer ngoài RBAC.
- Webhook subscription và signed delivery.
- Qdrant hoặc pgvector nếu muốn tách vector database khỏi Elasticsearch.
- Kafka analytics stream nếu muốn demo ordered high-throughput events.
- Kubernetes demo nếu muốn trình bày orchestration ngoài Docker Compose.
- Python/FastAPI analytics-ML service nếu muốn thêm forecasting/clustering.

Ghi chú: các mục expansion không bị cắt. Chúng chỉ được xếp sau core production để tránh build lệch thứ tự dependency.

## 3. Service Map Cần Tạo

| Service | Tech | Port | Vai trò trong timeline |
|---|---:|---:|---|
| `gateway-bff` | NestJS | 3000 | Entry point, JWT, tenant, rate limit, correlation ID, proxy, WebSocket |
| `identity-service` | ASP.NET Core 8 | 5001 | Register, login, refresh token, email verification |
| `workspace-service` | ASP.NET Core 8 | 5002 | Tenant, membership, RBAC, feature flags |
| `support-service` | ASP.NET Core 8 | 5003 | Customer, ticket lifecycle, SLA, audit, idempotency |
| `knowledge-service` | ASP.NET Core 8 | 5004 | Article, versioning, publish workflow, file metadata |
| `notification-service` | NestJS | 3001 | Email, in-app notification, WebSocket push, DLQ |
| `search-service` | NestJS | 3002 | Elasticsearch projection, indexing consumers, search APIs |
| `ai-service` | NestJS | 3003 | RAG, classify, summarize, suggest reply, AI logs |
| `campaign-service` | ASP.NET Core 8 | 5005 | Segment, campaign scheduling, follow-up sau resolve |

## 4. Week 1 - Source Foundation, Infra, Identity, Workspace

Mục tiêu tuần 1: repo chạy được, infra local ổn định, auth và tenant flow đủ để FE agent bắt đầu màn login/workspace.

### Ngày 1 - Khởi Tạo Source Và Monorepo

- Tạo repo/backend workspace `signaldesk-be`.
- Chốt cấu trúc `services/`, `libs/`, `infra/`, `.github/workflows/`.
- Tạo placeholder cho toàn bộ services:
  - NestJS: `gateway-bff`, `notification-service`, `search-service`, `ai-service`.
  - .NET: `identity-service`, `workspace-service`, `support-service`, `knowledge-service`, `campaign-service`.
- Với mỗi .NET service, tạo layout Clean Architecture:
  - `Domain`.
  - `Application`.
  - `Infrastructure`.
  - `API`.
- Với mỗi NestJS service, tạo module boundary tối thiểu:
  - `config`.
  - `health`.
  - `logging`.
  - `messaging` nếu service consume/publish async.
  - module nghiệp vụ chính của service.
- Tạo `libs/contracts/events` để chứa JSON Schema/event contract.
- Tạo `libs/dotnet/BuildingBlocks` cho tenant context, base entity, result pattern, outbox, audit, observability.
- Tạo `libs/node/common` cho config validation, logger, RabbitMQ helper, tracing helper.
- Chốt rule đặt tên env var, port, Docker network, service DNS name.
- Handoff cho FE:
  - Gửi danh sách base URL local.
  - Gửi danh sách route dự kiến qua gateway.
  - Chốt format lỗi API chung.

Deliverable cuối ngày:

- Repo có skeleton đầy đủ.
- Tất cả service có health endpoint tạm.
- FE agent có route map đầu tiên để bắt đầu mock integration.

### Ngày 2 - Local Infrastructure Và Database Baseline

- Tạo Docker Compose local cho:
  - PostgreSQL 16.
  - PgBouncer.
  - MongoDB 7.
  - Redis 7.
  - RabbitMQ 3.13 kèm management UI.
  - Elasticsearch 8.
  - MinIO.
  - Mailpit.
  - Ollama.
- Tạo Compose riêng cho observability hoặc bật dần:
  - Prometheus.
  - Grafana.
  - Jaeger hoặc Tempo.
  - Loki + Promtail.
- Chốt PostgreSQL multi-schema:
  - `identity`.
  - `workspace`.
  - `support`.
  - `knowledge`.
  - `campaign`.
  - `ops`.
- Tạo baseline migration hoặc migration plan cho `ops`:
  - `outbox_events`.
  - `inbox_messages`.
  - `idempotency_keys`.
  - `audit_logs`.
  - `dead_letters` hoặc DLQ tracking table nếu chọn lưu trong DB.
- Chốt RabbitMQ topology:
  - domain event exchange.
  - queue theo workload: notification, search-index, ai-tasks, campaign.
  - DLQ và retry policy.
- Chốt object storage:
  - Local dùng MinIO.
  - Production dùng Cloudflare R2 hoặc Supabase Storage nếu muốn tránh lưu file trên VPS.
- Handoff cho FE:
  - Xác nhận file upload sẽ dùng presigned URL, FE không upload binary qua backend API chính.

Deliverable cuối ngày:

- Local infra chạy được.
- Các service kết nối được dependency chính.
- Có sơ đồ runtime local và danh sách port chính xác.

### Ngày 3 - Building Blocks, Observability Nền Và Gateway V1

- Hoàn thiện .NET building blocks tối thiểu:
  - tenant context.
  - correlation ID propagation.
  - base entity và soft delete convention.
  - unit of work boundary.
  - validation pipeline.
  - outbox abstraction.
  - audit abstraction.
  - health check conventions.
- Hoàn thiện NestJS common tối thiểu:
  - config validation.
  - Winston structured logging.
  - OpenTelemetry bootstrap.
  - RabbitMQ client helper.
  - Redis helper.
- Tạo `gateway-bff` v1:
  - middleware correlation ID.
  - JWT verification placeholder hoặc public key based verification.
  - tenant resolution từ token.
  - reject tenant mismatch.
  - route map đến identity/workspace/support/knowledge/search/ai.
  - Redis token bucket rate limit ở mức skeleton.
  - `/api/health` aggregate readiness placeholder.
- Tạo OpenAPI/Swagger convention:
  - Mỗi service có docs riêng.
  - Gateway có docs tổng hợp hoặc route đến docs từng service.
- Handoff cho FE:
  - Chốt header bắt buộc: auth token, correlation ID optional, tenant context do gateway kiểm soát.
  - Chốt response envelope và error envelope.

Deliverable cuối ngày:

- Gateway có thể nhận request và forward đến health endpoint của downstream service.
- Logs có `correlation_id` và `tenant_id` nếu có auth context.

### Ngày 4 - Identity Service V1

- Tạo schema `identity`:
  - users.
  - refresh tokens.
  - email verification tokens.
  - password reset tokens theo scope auth đầy đủ.
- Implement nghiệp vụ chính:
  - register.
  - login.
  - refresh token rotation.
  - logout/revoke refresh token.
  - email verification flow.
  - password hash bằng BCrypt.
  - access token ngắn hạn, refresh token dài hạn.
- Tạo outbox events:
  - `identity.user.registered.v1`.
  - `identity.user.email-verified.v1`.
- Tạo cleanup job:
  - expired refresh tokens.
  - expired verification tokens.
- Tạo internal contract cho refresh:
  - identity gọi workspace để kiểm tra membership còn Active trước khi cấp access token mới.
- Handoff cho FE:
  - Chốt auth endpoints.
  - Chốt token lifetime.
  - Chốt flow refresh token.
  - Chốt lỗi khi membership bị revoked.

Deliverable cuối ngày:

- Đăng ký, đăng nhập, refresh token chạy qua gateway.
- Event user registered được ghi vào outbox.

### Ngày 5 - Workspace Service V1 Và Notification Foundation

- Tạo schema `workspace`:
  - tenants.
  - memberships.
  - roles.
  - permissions.
  - role permissions.
  - feature flags.
- Implement workspace flow:
  - consume `identity.user.registered.v1`.
  - auto-create Starter tenant.
  - tạo Admin membership cho owner.
  - seed default roles và permissions.
  - invite member.
  - remove member.
  - internal membership lookup cho identity refresh flow.
  - internal RBAC check cho downstream services.
- Tích hợp Redis cache:
  - user permissions.
  - feature flags.
  - cache invalidation khi membership/role đổi.
- Tạo `workspace.member.removed.v1`.
- Identity consume `workspace.member.removed.v1` để revoke refresh token trong tenant đó.
- Tạo `notification-service` foundation:
  - consume identity/workspace events.
  - Redis inbox dedup.
  - MongoDB templates và notification history.
  - email qua Mailpit local.
  - DLQ policy.
- Handoff cho FE:
  - Chốt endpoints tenant switch, current user, current membership, permissions.
  - FE có thể dựng workspace shell dựa trên auth + membership response.

Deliverable cuối tuần:

- User register -> tenant created -> welcome/verification email flow chạy end-to-end local.
- FE agent có auth/workspace contract ổn định.

### Weekend Buffer Tuần 1

- Viết seed ban đầu cho 2 tenants: TaskFlow và InvoiceFox.
- Viết integration smoke test cho auth -> workspace -> notification.
- Sửa naming, env, health, Docker issue trước khi sang support core.
- Cập nhật README BE phần local startup, service map, API docs URL.

## 5. Week 2 - Support Core, Ticket Lifecycle, Outbox Reliability

Mục tiêu tuần 2: ticket lifecycle chạy thật, event pipeline đáng tin, notification side effects không làm hỏng transactional flow.

### Ngày 6 - Support Service Source Và Schema

- Tạo `support-service` theo Clean Architecture + CQRS.
- Tạo schema `support`:
  - customers.
  - tickets.
  - ticket messages.
  - ticket status history.
  - ticket events nếu dùng scoped event sourcing cho timeline.
  - SLA policies.
  - ticket counters.
  - teams nếu cần assignment/team filter.
- Chốt aggregate root `Ticket`:
  - status.
  - assignment.
  - priority.
  - category.
  - sentiment.
  - SLA fields.
  - row version.
  - soft delete.
- Tạo command/query boundary:
  - create ticket.
  - assign ticket.
  - add message.
  - add internal note.
  - resolve.
  - close.
  - get detail.
  - list tickets.
  - ticket stats.
- Handoff cho FE:
  - Chốt ticket status enum, priority enum, message author type.
  - Chốt payload tạo ticket từ widget/workspace.

Deliverable cuối ngày:

- Support service có schema và API contract v1.
- FE có thể bắt đầu ticket list/detail UI bằng contract.

### Ngày 7 - Ticket Lifecycle Và Consistency Guard

- Implement create ticket:
  - upsert customer.
  - allocate `ticket_no` theo tenant.
  - tạo ticket.
  - tạo first message nếu request đến từ widget/customer.
  - ghi audit.
  - ghi outbox `support.ticket.created.v1`.
- Implement assign:
  - check RBAC.
  - update assigned agent.
  - increment row version.
  - ghi outbox `support.ticket.assigned.v1`.
- Implement add message/internal note:
  - enforce idempotency key cho public/customer message và agent reply.
  - internal note không đi vào search/AI retrieval phase 1.
  - ghi outbox `support.ticket.message-added.v1`.
- Implement resolve/close:
  - validate status transition.
  - set resolved/closed time.
  - ghi outbox tương ứng.
- Tích hợp optimistic concurrency:
  - mọi mutation quan trọng nhận expected version.
  - conflict trả lỗi rõ để FE refetch/merge.
- Handoff cho FE:
  - Chốt cách xử lý `409 Conflict`.
  - Chốt khi nào FE gửi idempotency key.

Deliverable cuối ngày:

- Ticket lifecycle chạy được bằng PostgreSQL source of truth.
- Duplicate create/add message được bảo vệ.

### Ngày 8 - Outbox Pattern Hoàn Chỉnh Cho .NET Services

- Hoàn thiện reusable OutboxPublisher trong .NET building blocks:
  - service name filter.
  - claim batch.
  - lock stale reclaim.
  - retry count.
  - backoff + jitter.
  - broker confirm.
  - mark published chỉ sau khi publish thành công.
  - dead-letter khi lỗi permanent hoặc quá retry.
- Tạo index cần thiết:
  - status.
  - next retry time.
  - claimed at.
  - service name.
- Áp dụng outbox cho:
  - identity.
  - workspace.
  - support.
  - knowledge sau khi tạo ở tuần 3.
  - campaign khi triển khai ngày 16.
- Chuẩn hóa event envelope:
  - event ID.
  - event type.
  - tenant ID.
  - correlation ID.
  - causation ID.
  - aggregate type/id/version.
  - actor.
  - payload.
- Tạo JSON Schema cho domain events phase 1:
  - identity user events.
  - workspace tenant/member events.
  - support ticket events.
- Handoff cho FE:
  - Không có thay đổi trực tiếp, nhưng chốt correlation ID để debug request từ FE.

Deliverable cuối ngày:

- Outbox event từ support được publish sang RabbitMQ ổn định.
- Có DLQ và cách xem lỗi consumer/publisher.

### Ngày 9 - Notification Consumers Cho Support Events

- Mở rộng `notification-service`:
  - consume `support.ticket.created.v1`.
  - consume `support.ticket.assigned.v1`.
  - consume `support.ticket.message-added.v1`.
  - consume `support.ticket.resolved.v1`.
  - consume `support.ticket.sla-breached.v1`.
- Giữ Redis inbox dedup TTL 7 ngày.
- Thêm null agent guard:
  - ticket mới từ widget có thể chưa assigned agent.
  - không crash khi `assigned_agent_id` null.
- Tạo email dedupe key:
  - chống gửi lại email khi replay event.
- Tạo in-app notification history trong MongoDB.
- Tạo DLQ alert/log rõ ràng.
- Handoff cho FE:
  - Chốt shape in-app notification.
  - Chốt unread count endpoint nếu FE cần.

Deliverable cuối ngày:

- Ticket event tạo email/in-app notification local qua Mailpit/Mongo.
- Replay event không gửi trùng email.

### Ngày 10 - SLA, Audit Timeline, Read API Và Test Tuần 2

- Implement SLA policy:
  - response due.
  - resolution due.
  - business hours rule theo tenant.
- Hangfire job:
  - scan ticket sắp breach/breached.
  - publish `support.ticket.sla-breached.v1`.
- Audit:
  - status change.
  - assignment change.
  - message added.
  - AI derived updates sau này.
- Read APIs:
  - ticket detail từ PostgreSQL.
  - ticket list từ PostgreSQL tạm thời, sau đó chuyển sang Elasticsearch ở tuần 3.
  - customer profile.
  - ticket stats.
- Test:
  - unit test domain transition.
  - integration test create ticket -> outbox -> RabbitMQ -> notification.
  - idempotency replay.
  - optimistic conflict.
- Handoff cho FE:
  - API docs support v1 stable.
  - FE có thể làm ticket list/detail/reply/resolve trước khi search xong.

Deliverable cuối tuần:

- Support core đủ để demo create -> assign -> reply -> resolve.
- Event pipeline support -> notification chạy thật.

### Weekend Buffer Tuần 2

- Dọn API docs và event docs.
- Sửa lỗi integration trước khi thêm knowledge/search/AI.
- Seed thêm ticket/messages realistic cho TaskFlow và InvoiceFox.
- Chuẩn bị WebSocket event push sớm nếu muốn giảm tải cho ngày 14.

## 6. Week 3 - Knowledge, Search, Realtime, AI v1

Mục tiêu tuần 3: KB publish được index, ticket/article tìm kiếm được, AI có RAG và agent assist v1.

### Ngày 11 - Knowledge Service V1

- Tạo `knowledge-service` theo Clean Architecture.
- Tạo schema `knowledge`:
  - articles.
  - article versions.
  - categories.
  - article attachments.
- Implement article workflow:
  - create draft.
  - update draft.
  - publish.
  - unpublish.
  - soft delete.
  - version snapshot khi publish.
- Tích hợp object storage:
  - presigned upload URL.
  - confirm upload.
  - attachment metadata.
  - local MinIO.
  - production provider R2 hoặc Supabase Storage.
- Publish outbox events:
  - `knowledge.article.published.v1`.
  - `knowledge.article.unpublished.v1`.
- Handoff cho FE:
  - Chốt article status enum.
  - Chốt slug, category, publish response.
  - Chốt flow upload file bằng presigned URL.

Deliverable cuối ngày:

- Admin có thể tạo/publish article qua API.
- Article publish tạo event để search-service index.

### Ngày 12 - Search Service Indexing

- Tạo Elasticsearch indices:
  - `tickets_v1`.
  - `kb_articles_v1`.
- Tạo `search-service` consumers:
  - ticket created.
  - ticket assigned.
  - ticket message added.
  - ticket resolved.
  - ticket closed.
  - AI summary completed.
  - article published.
  - article unpublished.
- Dùng Redis inbox dedup cho search consumers.
- Áp dụng stale event guard:
  - chỉ update projection nếu event aggregate version mới hơn hoặc bằng version trong ES.
- Không index internal notes.
- Với article published:
  - upsert text fields trước.
  - schedule embedding job sau, không block publish request.
- Tạo projection freshness metric:
  - occurred at -> indexed at.
- Handoff cho FE:
  - Chốt ticket search/list endpoint chuyển dần sang search-service.
  - Chốt eventual consistency: list/search có thể trễ vài giây, detail luôn đọc PostgreSQL.

Deliverable cuối ngày:

- Ticket và KB article được sync sang Elasticsearch.
- Reassign ticket không làm ES stale agent ID.

### Ngày 13 - Search APIs Và Hybrid Retrieval V1

- Expose search endpoints qua gateway:
  - ticket search.
  - KB article search.
  - unified search.
- Implement keyword search trước:
  - tenant filter bắt buộc.
  - status/category/priority filter.
  - pagination.
  - highlight/snippet.
- Thêm semantic/hybrid sau khi keyword search đã ổn:
  - article chunks.
  - embedding job.
  - dense vector trong Elasticsearch.
  - fallback keyword nếu embedding provider fail.
- Chốt search ownership:
  - `knowledge-service` own article source.
  - `search-service` own ES projection.
  - `ai-service` chỉ đọc KB projection cho retrieval.
- Handoff cho FE:
  - Search response shape stable.
  - Chốt empty state, stale state, loading state theo contract.

Deliverable cuối ngày:

- FE có thể gọi ticket search và KB search qua gateway.
- Search có tenant isolation bắt buộc.

### Ngày 14 - Gateway WebSocket Và Realtime

- Tạo WebSocket namespace trong `gateway-bff`.
- Authenticate khi connect.
- Join rooms:
  - user room.
  - tenant room.
  - ticket room.
- Dùng Redis adapter để sẵn sàng scale nhiều instance.
- Presence tracking:
  - Redis TTL.
  - heartbeat.
  - disconnect cleanup best effort.
- Typing indicator:
  - ticket room.
  - short TTL.
- Live ticket update:
  - support event -> notification/gateway push hoặc gateway pull từ notification channel tùy implement.
- Collision detection:
  - Redis advisory lock cho editing/replying.
  - PostgreSQL row version vẫn là data guard cuối cùng.
- Handoff cho FE:
  - Chốt event names WebSocket.
  - Chốt room subscription flow.
  - Chốt cách FE hiển thị presence/typing/conflict.

Deliverable cuối ngày:

- Workspace có thể nhận live update, presence, typing indicator và lock/conflict signal.
- Realtime path có auth, tenant isolation và Redis adapter.

### Ngày 15 - AI Service V1

- Tạo `ai-service` NestJS.
- Tạo MongoDB collections:
  - `ai_runs`.
  - `chat_sessions`.
  - `customer_memory`.
  - `inbox_messages`.
- Tạo LLM provider abstraction:
  - OpenAI primary.
  - Ollama fallback.
  - timeout/circuit breaker.
- Tạo RAG v1:
  - retrieve KB theo tenant.
  - grounding context.
  - citation.
  - confidence scoring.
  - threshold 0.75.
  - dưới threshold thì escalate/human handoff, không bịa.
- Tạo AI ticket enrichment:
  - consume `support.ticket.created.v1`.
  - classify category, priority, sentiment, tags.
  - publish `ai.classification.completed.v1`.
  - support-service consume event và update AI derived fields.
- Tạo summary:
  - consume `support.ticket.resolved.v1`.
  - summarize thread.
  - update customer memory.
  - publish `ai.summary.completed.v1`.
- Tạo agent assist endpoint:
  - suggest reply.
  - cache Redis ngắn hạn nếu cùng ticket chưa đổi.
- Handoff cho FE:
  - Chốt endpoint ask AI/RAG.
  - Chốt response có answer, citations, confidence, escalated.
  - Chốt suggested reply API cho agent workspace.

Deliverable cuối tuần:

- Article publish -> search index -> AI retrieve.
- Ticket created -> AI classify -> support ticket updated.
- Ticket resolved -> summary/customer memory.
- FE agent có contract cho AI panel/chatbot.

### Weekend Buffer Tuần 3

- Test end-to-end:
  - KB publish -> search -> RAG answer.
  - Ticket create -> classify -> notification -> search.
  - Ticket resolve -> summary -> customer memory.
- Kiểm tra tenant isolation trong search và AI.
- Fix AI latency/fallback.
- Bổ sung demo data KB chất lượng để RAG trả lời được.

## 7. Week 4 - Campaign, Performance, Production Deploy, Observability

Mục tiêu tuần 4: hệ thống chạy production, có monitoring, có kết quả load test và đủ chất liệu demo/CV.

### Ngày 16 - Campaign Service Và Follow-Up Automation

- Tạo `campaign-service` theo Clean Architecture.
- Tạo schema `campaign`:
  - campaign definitions.
  - campaign dispatches.
  - segments.
  - delivery attempts nếu cần tracking chi tiết.
- Implement segment/follow-up rule:
  - segment theo tenant.
  - trigger theo `support.ticket.resolved.v1`.
  - rule chọn customer/ticket phù hợp.
- Tích hợp Hangfire:
  - schedule follow-up job.
  - retry failed dispatch.
  - persistent job state.
- Publish `campaign.dispatch.requested.v1`.
- Notification consume campaign dispatch:
  - render email/in-app template.
  - dedupe theo event/recipient.
  - retry và DLQ.
- Thêm idempotency:
  - unique dispatch theo campaign + ticket/customer.
  - replay event không duplicate-send.
- Thêm per-campaign advisory lock.
- Thêm throttle email theo tenant.
- Thêm campaign metrics:
  - scheduled.
  - dispatched.
  - sent.
  - failed.
  - duplicate skipped.
- Handoff cho FE:
  - Chốt campaign definition API.
  - Chốt segment rule shape.
  - Chốt campaign dispatch/status fields.

Deliverable cuối ngày:

- Ticket resolved có thể trigger follow-up campaign.
- Campaign flow replay-safe và có tracking.

### Ngày 17 - Caching, Backpressure, Reliability, Security

- Redis cache-aside:
  - ticket detail.
  - KB article.
  - user permissions.
  - feature flags.
  - dashboard widgets.
- Cache invalidation:
  - ticket update evict ticket cache.
  - article publish evict KB cache.
  - membership/role change evict permission cache.
  - campaign update evict campaign/segment cache.
- Cache stampede prevention:
  - mutex lock.
  - double-check after acquiring lock.
- Gateway rate limit:
  - per tenant.
  - per route group.
  - stricter limit cho AI/search/campaign endpoints.
- AI backpressure:
  - prefetch limit.
  - per-tenant AI job rate limiter.
  - circuit breaker for external AI provider.
  - fallback Ollama hoặc human handoff.
- Campaign/notification backpressure:
  - per-tenant email throttle.
  - queue depth alert.
  - DLQ alert.
- PgBouncer:
  - tất cả services connect qua PgBouncer.
  - theo dõi pool wait time.
- Hoàn thiện health endpoints:
  - live.
  - ready.
  - gateway aggregate health.
- Chuẩn hóa retry policy:
  - RabbitMQ consumers.
  - HTTP clients.
  - external AI/email/object storage.
- DLQ runbook:
  - trace bằng correlation ID.
  - phân loại transient/permanent/poison.
  - replay an toàn nhờ inbox/idempotency.
- Security pass:
  - tenant mismatch reject.
  - mandatory tenant filter ở PostgreSQL, Redis, ES, AI retrieval.
  - refresh token revoke sau membership removed.
  - secret management cho production.
  - CORS theo domain FE production.
  - request body size limit.
  - file upload content type/size rule.
- Sentry hoặc equivalent:
  - backend exception tracking.
  - environment tagging.
- Handoff cho FE:
  - Chốt lỗi rate limit và retry-after semantics.
  - Chốt CORS/domain production.
  - Chốt auth cookie/header strategy nếu FE cần đổi.

Deliverable cuối ngày:

- Cache/rate limit/backpressure đã bật ở path chính.
- Readiness checks đáng tin.
- Các lỗi production phổ biến có guard rõ.

### Ngày 18 - Testing, Load Test, Demo Data

- Unit tests:
  - ticket status transitions.
  - RBAC/permission policy.
  - idempotency behavior.
  - confidence scoring.
- Integration tests:
  - register -> tenant created -> notification.
  - create ticket -> outbox -> notification/search/AI.
  - article publish -> search index -> RAG retrieval.
  - membership removed -> refresh token rejected.
  - replay event -> no duplicate email/index corruption.
- Contract tests:
  - Gateway -> support-service.
  - RabbitMQ event schema compatibility.
  - OpenAPI docs not drifting from implemented endpoints.
- k6 load test round 1:
  - auth.
  - ticket create/list/detail.
  - add message.
  - search.
  - AI endpoint with controlled rate.
- Optimize:
  - top slow SQL queries.
  - missing indexes.
  - ES query latency.
  - Redis cache hit rate.
- Demo data:
  - 2 tenants.
  - 10 agents.
  - 100+ tickets.
  - 300+ messages.
  - 30+ KB articles.
  - 20+ RAG test questions.
- Handoff cho FE:
  - Cung cấp demo accounts.
  - Cung cấp seed data assumptions để FE demo giống BE.

Deliverable cuối ngày:

- Test suite đủ tin cậy cho deploy.
- Có số liệu load test đầu tiên và danh sách fix performance.

### Ngày 19 - Production Deploy

- Tạo Dockerfile production cho từng service.
- Tạo Docker Compose production:
  - named volumes.
  - private network.
  - health checks.
  - restart policy.
  - resource limits.
  - environment separation.
- Nginx:
  - reverse proxy.
  - SSL bằng Let's Encrypt.
  - gzip.
  - request size limit.
  - caching headers phù hợp cho static/docs nếu có.
- Production storage:
  - MinIO nếu self-host.
  - hoặc Cloudflare R2/Supabase Storage nếu muốn giảm rủi ro disk server.
- Database:
  - migration strategy.
  - backup plan.
  - restore rehearsal.
- GitHub Actions:
  - CI: lint, test, build.
  - Docker build/push.
  - SSH deploy.
  - smoke test sau deploy.
- UptimeRobot:
  - monitor gateway health.
  - monitor public API docs hoặc app URL.
- Handoff cho FE:
  - Cung cấp production API base URL.
  - Chốt allowed origins.
  - Chốt health/status endpoint FE có thể kiểm tra.

Deliverable cuối ngày:

- Backend có URL production chạy thật.
- Deploy pipeline chạy lại được mà không thao tác thủ công nhiều.

### Ngày 20 - Observability, Final Hardening, Handoff

- OpenTelemetry:
  - .NET services.
  - NestJS services.
  - trace propagation qua HTTP và RabbitMQ.
  - correlation ID gắn vào span/log.
- Prometheus metrics:
  - request duration.
  - error rate.
  - queue depth.
  - DLQ count.
  - idempotency replay/conflict.
  - cache hit/miss.
  - AI latency/confidence.
  - search latency.
  - projection freshness.
  - PgBouncer pool wait.
  - circuit breaker state.
- Grafana dashboards:
  - API Health.
  - Queue Health.
  - AI Pipeline.
  - Tenant Health.
  - Infrastructure.
- Loki/Promtail:
  - structured log search bằng correlation ID.
- Alerts:
  - p95 latency.
  - error rate.
  - queue depth.
  - oldest message age.
  - DLQ count.
  - AI provider failure.
  - disk/CPU/memory.
- k6 final:
  - mixed scenario.
  - document p95, error rate, throughput.
- Documentation:
  - README backend.
  - architecture diagram.
  - service ownership.
  - event contract matrix.
  - deploy runbook.
  - DLQ replay runbook.
  - demo scenario.
- FE final handoff:
  - API docs.
  - production URL.
  - demo accounts.
  - WebSocket events.
  - known eventual consistency behavior.
  - danh sách open items nếu có phần nào cần kéo dài thêm.

Deliverable cuối dự án:

- Production backend chạy thật.
- Có monitoring dashboard.
- Có demo data.
- Có tài liệu đủ để đưa vào portfolio/CV.

## 8. Expansion Backlog Không Cắt Scope

Các mục dưới đây vẫn thuộc full-scope backlog. Nếu chưa kịp trong 4 tuần, tiếp tục triển khai sau production hardening theo đúng thứ tự này, không thay thế bằng bản rút gọn.

### Identity Và Policy Expansion

- Hoàn thiện OIDC-compatible endpoints:
  - discovery.
  - JWKS.
  - authorize/token/logout/userinfo nếu muốn mô phỏng provider chuẩn hơn.
- Bổ sung ABAC policy layer:
  - tenant plan.
  - membership status.
  - ticket ownership.
  - team/department.
  - feature flags.
  - business hours.
- Tăng độ sâu audit:
  - audit every admin/security action.
  - export audit log theo tenant.

### Integration Expansion

- Webhook system:
  - webhook subscriptions.
  - signed payload bằng HMAC.
  - delivery attempts.
  - retry/backoff.
  - DLQ/manual replay.
  - tenant event isolation.
- Multi-channel notification:
  - email.
  - in-app.
  - Web Push/FCM nếu cần.
  - provider abstraction cho SendGrid/Resend/SparkPost/SMTP.

### Data/Search/AI Expansion

- Tách vector database:
  - Qdrant cho production-grade vector store.
  - hoặc pgvector nếu muốn ít infra hơn.
- AI evaluation set:
  - test questions theo tenant.
  - expected citations.
  - confidence distribution.
  - hallucination/escalation checks.
- AI agent mode có tool calling giới hạn:
  - search knowledge base.
  - get ticket snapshot.
  - get customer memory.
  - draft reply.
  - suggest internal note.
- Python/FastAPI analytics-ML service:
  - forecasting.
  - clustering.
  - heavy analytics ngoài request path.

### Event/Infra Expansion

- Kafka analytics stream:
  - chỉ thêm sau khi có lý do đo được hoặc muốn demo ordered analytics events.
  - giữ RabbitMQ cho workflow/business events nếu vẫn phù hợp.
- Kubernetes demo:
  - Minikube hoặc cheap cluster.
  - manifests/helm theo service.
  - HPA demo cho gateway/search/AI.
- Observability nâng cao:
  - SLO burn-rate alerts.
  - tenant health dashboard sâu hơn.
  - queue oldest message age alert.
  - error budget policy trong README.

## 9. Backend/Frontend Handoff Checkpoints

| Thời điểm | BE phải bàn giao cho FE agent |
|---|---|
| Cuối ngày 1 | Base route map, port map, response/error envelope dự kiến |
| Cuối ngày 3 | Gateway base URL, auth header rule, tenant/correlation behavior |
| Cuối ngày 5 | Auth, current user, current tenant, membership, permission contracts |
| Cuối ngày 7 | Ticket status/priority/message models, mutation contract, conflict rule |
| Cuối ngày 10 | Ticket list/detail/reply/resolve APIs ổn định |
| Cuối ngày 11 | Knowledge article/category/upload/publish contracts |
| Cuối ngày 13 | Search APIs và eventual consistency notes |
| Cuối ngày 14 | WebSocket event names, presence, typing, lock/conflict behavior |
| Cuối ngày 15 | AI RAG/suggest-reply response shape |
| Cuối ngày 18 | Demo accounts, seed data, known API limitations |
| Cuối ngày 19 | Production API URL, CORS origins, health endpoint |
| Cuối ngày 20 | Final API docs, demo script, monitoring URL nếu share được |

## 10. Definition Of Done Theo Phase

### Foundation Done

- Repo có đủ service skeleton.
- Local infra chạy được.
- Mỗi service có health endpoint.
- Logs có correlation ID.
- CI build được skeleton.

### Auth/Workspace Done

- Register/login/refresh chạy qua gateway.
- Tenant auto-created sau register.
- RBAC seed và permission cache hoạt động.
- Remove member làm refresh token bị revoke.
- Notification gửi email local qua Mailpit.

### Support Done

- Ticket lifecycle end-to-end.
- `ticket_no` unique per tenant.
- Idempotency cho create/add message.
- Optimistic concurrency trả conflict đúng.
- Audit timeline có dữ liệu.
- Support events publish qua outbox.

### Knowledge/Search Done

- Article publish tạo immutable version.
- Attachment metadata và presigned upload flow sẵn sàng.
- Article/ticket events sync sang Elasticsearch.
- Tenant filter bắt buộc trong mọi search query.
- Stale event không overwrite projection mới.

### AI Done

- RAG chỉ retrieve KB đúng tenant.
- Response có citation.
- Confidence dưới threshold thì escalate.
- Ticket created được auto-classify.
- Ticket resolved được summarize.
- Mọi AI run được log vào MongoDB.

### Campaign Done

- Campaign definition và segment rule có API rõ.
- Ticket resolved trigger được follow-up workflow.
- Dispatch idempotent theo campaign + ticket/customer.
- Notification consume campaign dispatch không gửi trùng khi replay.
- Campaign metrics có scheduled, sent, failed, duplicate skipped.

### Production Done

- Backend chạy trên VPS bằng Docker Compose production.
- Nginx + SSL hoạt động.
- CI/CD deploy được.
- Health check và smoke test pass.
- Grafana có dashboard đầy đủ theo scope production.
- UptimeRobot monitor bật.

## 11. Risk Register

| Risk | Dấu hiệu | Cách xử lý |
|---|---|---|
| Quá nhiều service cho 2-4 tuần | Mất quá 3 ngày chỉ scaffold/source | Giữ đủ service name nhưng implement thin slice, không polish sớm |
| Outbox/Inbox mất thời gian | Event pipeline chưa ổn ở cuối tuần 2 | Ưu tiên support -> notification/search, AI để sau |
| AI latency/cost | Queue `ai-tasks` phình, request timeout | Tách AI khỏi request path, dùng queue/fallback/handoff, sau đó tối ưu throughput |
| Search phức tạp | Hybrid chưa ổn | Triển khai keyword search trước rồi hoàn thiện semantic/hybrid ngay sau khi projection ổn |
| FE bị block | Contract đổi liên tục | Freeze response shape theo tuần, thêm field optional thay vì breaking |
| Deploy trễ | Local chạy nhưng VPS lỗi | Bắt đầu Docker production từ ngày 16, không để đến ngày cuối |
| Tenant isolation bug | Data cross-tenant trong cache/search | Test bắt buộc bằng 2 tenants và JWT mismatch case |

## 12. Thứ Tự Ưu Tiên Khi Mỗi Ngày Bị Thiếu Thời Gian

1. Làm path chạy được từ Gateway đến service.
2. Làm source of truth đúng trong PostgreSQL.
3. Ghi outbox event đúng.
4. Consumer idempotent.
5. API contract ổn định cho FE.
6. Test integration cho path đó.
7. Observability metric/log cho path đó.
8. Polish, cache, dashboard, docs.

## 13. Demo Flow Cuối Dự Án

Backend nên hỗ trợ ít nhất 4 demo flow:

1. Tenant isolation:
   - Login TaskFlow.
   - Xem tickets TaskFlow.
   - Dùng tenant/token mismatch bị gateway reject.

2. Ticket event pipeline:
   - Create ticket.
   - Outbox publish event.
   - Notification gửi email/in-app.
   - Search projection cập nhật.
   - AI classify cập nhật ticket.

3. Knowledge/RAG:
   - Publish article.
   - Search-service index article.
   - AI trả lời có citation.
   - Câu hỏi ngoài KB bị escalate.

4. Observability:
   - Mở Grafana API dashboard.
   - Mở queue dashboard.
   - Trace một request qua gateway -> support -> RabbitMQ -> AI/notification/search.
   - Tìm log bằng correlation ID.

## 14. Ghi Chú Chốt

- Bản 4 tuần là timeline mục tiêu để có backend production-ready kèm campaign, realtime, AI, monitoring và deploy.
- Không có bản cắt scope trong tài liệu này. Nếu trễ, kéo dài mốc thời gian và tiếp tục đúng thứ tự dependency.
- Với dự án cá nhân, thứ tự làm vẫn quan trọng: core transactional flow trước, event/AI/search sau, production hardening sau cùng.
- Sau production core, tiếp tục expansion backlog theo thứ tự: OIDC/ABAC -> webhook -> AI evaluation -> Qdrant/pgvector -> Kafka analytics -> Kubernetes demo -> Python ML service.

# Gateway Route Map

Gateway base URL:

```text
http://localhost:3000
```

Implemented Day 3 route ownership:

| Gateway Prefix | Downstream Service | Local Fallback | Environment Override | Notes |
| --- | --- | --- | --- | --- |
| /api/auth | identity-service | http://localhost:5001 | IDENTITY_SERVICE_URL | Register, login, refresh are public placeholders; other auth paths are protected |
| /api/users | identity-service | http://localhost:5001 | IDENTITY_SERVICE_URL | Current user and account profile |
| /api/workspace | workspace-service | http://localhost:5002 | WORKSPACE_SERVICE_URL | Tenant, membership, RBAC, feature flags |
| /api/support | support-service | http://localhost:5003 | SUPPORT_SERVICE_URL | Customers, tickets, messages, SLA |
| /api/knowledge | knowledge-service | http://localhost:5004 | KNOWLEDGE_SERVICE_URL | Articles, versions, attachments |
| /api/notifications | notification-service | http://localhost:3001 | NOTIFICATION_SERVICE_URL | In-app notifications and unread count |
| /api/search | search-service | http://localhost:3002 | SEARCH_SERVICE_URL | Ticket and article search |
| /api/ai | ai-service | http://localhost:3003 | AI_SERVICE_URL | RAG, classification, summaries, suggestions |
| /api/campaigns | campaign-service | http://localhost:5005 | CAMPAIGN_SERVICE_URL | Campaign definitions and dispatch status |
| /api/health | gateway-bff | n/a | n/a | Aggregate health placeholder; downstream services are listed as `not_checked` |

`/ws` remains reserved for the gateway realtime namespace and is not implemented by the Day 3 proxy route map.

## Required Request Context

```text
Authorization: Bearer <access-token>
X-Correlation-Id: optional client-generated correlation ID
X-Tenant-Id: forwarded by the gateway after auth; client-provided tenant IDs are mismatch-checked
```

Tenant context is resolved by the gateway from the authenticated token. Client-provided tenant IDs in headers, route params, query, or body must not be trusted as source of truth.

## Day 3 Proxy Behavior

- `GET /health/live` and `GET /health/ready` stay outside the `/api` prefix.
- `GET /api/health` is public and returns an aggregate placeholder without polling downstream services.
- Protected routes require a placeholder bearer token with trusted claims for tenant propagation.
- Gateway rate limiting runs after auth; protected routes are keyed by trusted token tenant ID, public routes by route group and IP/header fallback, and exceeded limits return HTTP 429 / `RATE_LIMITED`.
- The proxy strips the gateway-owned `/api` prefix before forwarding route-map-owned paths to downstream services, then forwards `Authorization`, `X-Correlation-Id`, trusted `X-Tenant-Id`, and `Content-Type` when present.
- Gateway readiness checks Redis by TCP and verifies downstream URL configuration; set `GATEWAY_READY_CHECK_DOWNSTREAMS=true` to additionally call downstream `/health/ready` endpoints.

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

## Day 4 Public Auth Routes

Day 4 adds identity endpoints through the gateway. All public routes skip auth:

| Gateway Route | Downstream Route | Method | Auth | Purpose |
| --- | --- | --- | --- | --- |
| `/api/auth/register` | `/auth/register` | POST | public | Register a new user |
| `/api/auth/login` | `/auth/login` | POST | public | Login with credentials |
| `/api/auth/refresh` | `/auth/refresh` | POST | public | Refresh access token |
| `/api/auth/verify-email` | `/auth/verify-email` | POST | public | Verify email with token |
| `/api/auth/password-reset/request` | `/auth/password-reset/request` | POST | public | Request password reset |
| `/api/auth/password-reset/confirm` | `/auth/password-reset/confirm` | POST | public | Confirm password reset |

Protected identity endpoints:

| Gateway Route | Downstream Route | Method | Auth | Purpose |
| --- | --- | --- | --- | --- |
| `/api/auth/logout` | `/auth/logout` | POST | protected | Revoke refresh token |
| `/api/auth/me` | `/auth/me` | GET | protected | Current user session info |

`GET /api/auth/me` is **deferred through the gateway** for tenant-less tokens. The gateway verifier requires `tenantId` in the JWT, but Day 4 identity tokens can be issued without tenant context (when no `TenantId` is provided during register/login). The gateway guard rejects tokens without `tenantId` before they reach the identity service. The identity service's `/auth/me` endpoint is fully implemented with RS256 validation and works for tokens that include `tenantId`. **Day 5/Day 6 blocker**: the gateway's `auth-claim.ts` and `gateway-auth.guard.ts` need route-specific tenant-less handling (outside Task D owned scope) to allow `/auth/me` through the gateway for tenant-less tokens. Full account profile remains available under `/api/users` for future workspace integration.

Framework binding failures (malformed JSON, invalid GUIDs, missing required fields) on identity endpoints return the documented `VALIDATION_ERROR` envelope. Unexpected runtime exceptions return `INTERNAL_ERROR` with HTTP 500.
- Gateway rate limiting runs after auth; protected routes are keyed by trusted token tenant ID, public routes by route group and IP/header fallback, and exceeded limits return HTTP 429 / `RATE_LIMITED`.
- The proxy strips the gateway-owned `/api` prefix before forwarding route-map-owned paths to downstream services, then forwards `Authorization`, `X-Correlation-Id`, trusted `X-Tenant-Id`, and `Content-Type` when present.
- Gateway readiness checks Redis by TCP and verifies downstream URL configuration; set `GATEWAY_READY_CHECK_DOWNSTREAMS=true` to additionally call downstream `/health/ready` endpoints.

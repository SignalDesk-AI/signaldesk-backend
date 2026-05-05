# Gateway Route Map

Gateway base URL:

```text
http://localhost:3000
```

Planned route ownership:

| Gateway Prefix | Downstream Service | Notes |
| --- | --- | --- |
| /api/auth | identity-service | Register, login, refresh, logout |
| /api/users | identity-service | Current user and account profile |
| /api/workspace | workspace-service | Tenant, membership, RBAC, feature flags |
| /api/support | support-service | Customers, tickets, messages, SLA |
| /api/knowledge | knowledge-service | Articles, versions, attachments |
| /api/notifications | notification-service | In-app notifications and unread count |
| /api/search | search-service | Ticket and article search |
| /api/ai | ai-service | RAG, classification, summaries, suggestions |
| /api/campaigns | campaign-service | Campaign definitions and dispatch status |
| /ws | gateway-bff | WebSocket namespace for realtime |

## Required Request Context

```text
Authorization: Bearer <access-token>
X-Correlation-Id: optional client-generated correlation ID
```

Tenant context is resolved by the gateway from the authenticated token. Client-provided tenant IDs must not be trusted as source of truth.

# API Error Envelope

All services should converge on this error format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Request is invalid",
    "details": [],
    "correlationId": "..."
  }
}
```

## Common Error Codes

| Code | HTTP Status | Meaning |
| --- | ---: | --- |
| VALIDATION_ERROR | 400 | Request payload or query is invalid |
| UNAUTHENTICATED | 401 | Missing or invalid access token |
| FORBIDDEN | 403 | Authenticated user lacks permission or tenant mismatch |
| NOT_FOUND | 404 | Resource does not exist in the current tenant |
| CONFLICT | 409 | Optimistic concurrency or idempotency conflict |
| RATE_LIMITED | 429 | Tenant or route limit exceeded |
| INTERNAL_ERROR | 500 | Unexpected server error |

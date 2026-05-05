# API Response Envelopes

All client-facing services should converge on these response envelopes. Internal
service-to-service APIs may use narrower contracts, but gateway-facing responses
should keep the same top-level shape so FE integration can be predictable.

## Success Envelope

Use this format for single-resource or command responses:

```json
{
  "data": {
    "id": "..."
  },
  "meta": {
    "correlationId": "..."
  }
}
```

`data` contains the endpoint-specific payload. For commands that do not return a
resource, return a small status payload instead of `null`, for example:

```json
{
  "data": {
    "accepted": true
  },
  "meta": {
    "correlationId": "..."
  }
}
```

## List Envelope

Use this format for paginated list responses:

```json
{
  "data": [
    {
      "id": "..."
    }
  ],
  "meta": {
    "correlationId": "...",
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalItems": 125,
      "totalPages": 7
    }
  }
}
```

Cursor-based endpoints may replace `pagination` with:

```json
{
  "nextCursor": "...",
  "hasMore": true
}
```

## Error Envelope

Use this format for errors:

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

Error responses should not include a top-level `data` property.

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

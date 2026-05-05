# Service Port Map

| Service | Local URL | Port |
| --- | --- | ---: |
| gateway-bff | http://localhost:3000 | 3000 |
| notification-service | http://localhost:3001 | 3001 |
| search-service | http://localhost:3002 | 3002 |
| ai-service | http://localhost:3003 | 3003 |
| identity-service | http://localhost:5001 | 5001 |
| workspace-service | http://localhost:5002 | 5002 |
| support-service | http://localhost:5003 | 5003 |
| knowledge-service | http://localhost:5004 | 5004 |
| campaign-service | http://localhost:5005 | 5005 |

## Health Endpoints

Every service exposes temporary Day 1 health endpoints:

```text
GET /health/live
GET /health/ready
```

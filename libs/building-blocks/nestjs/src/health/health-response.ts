export interface HealthResponse {
  status: 'ok' | 'degraded' | 'down';
  service: string;
  checkedAt: string;
}

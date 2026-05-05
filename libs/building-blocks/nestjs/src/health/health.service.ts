import { Injectable } from '@nestjs/common';

import { HealthResponse } from './health-response';

@Injectable()
export class HealthService {
  live(service: string): HealthResponse {
    return {
      status: 'ok',
      service,
      checkedAt: new Date().toISOString(),
    };
  }

  ready(service: string): HealthResponse {
    return this.live(service);
  }
}

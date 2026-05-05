import { Controller, Get } from '@nestjs/common';

const serviceName = 'gateway-bff';

@Controller('health')
export class HealthController {
  @Get('live')
  live() {
    return {
      status: 'ok',
      service: serviceName,
      checkedAt: new Date().toISOString(),
    };
  }

  @Get('ready')
  ready() {
    return {
      status: 'ok',
      service: serviceName,
      checkedAt: new Date().toISOString(),
    };
  }
}

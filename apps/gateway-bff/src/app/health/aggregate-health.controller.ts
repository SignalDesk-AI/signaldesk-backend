import { Controller, Get } from '@nestjs/common';

import { aggregateHealthPlaceholder } from './aggregate-health';

@Controller('health')
export class AggregateHealthController {
  @Get()
  aggregate() {
    return aggregateHealthPlaceholder();
  }
}

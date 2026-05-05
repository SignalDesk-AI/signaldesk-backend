import { Module } from '@nestjs/common';

import { TenantContextService } from './tenant-context.service';

@Module({
  providers: [TenantContextService],
  exports: [TenantContextService],
})
export class TenantModule {}

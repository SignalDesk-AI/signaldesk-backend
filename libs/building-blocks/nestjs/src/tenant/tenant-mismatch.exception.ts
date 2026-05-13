import { HttpStatus } from '@nestjs/common';

import { ApiException } from '../errors';

export class TenantMismatchException extends ApiException {
  constructor(expectedTenantId: string | undefined, actualTenantId: string | undefined) {
    super('FORBIDDEN', 'Tenant mismatch', HttpStatus.FORBIDDEN, [
      {
        path: 'tenantId',
        expectedTenantId,
        actualTenantId,
      },
    ]);
  }
}

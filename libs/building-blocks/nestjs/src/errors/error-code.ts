import { HttpStatus } from '@nestjs/common';

import { ErrorCode } from './error-envelope';

const statusCodeMap: Partial<Record<HttpStatus, ErrorCode>> = {
  [HttpStatus.BAD_REQUEST]: 'VALIDATION_ERROR',
  [HttpStatus.UNAUTHORIZED]: 'UNAUTHENTICATED',
  [HttpStatus.FORBIDDEN]: 'FORBIDDEN',
  [HttpStatus.NOT_FOUND]: 'NOT_FOUND',
  [HttpStatus.CONFLICT]: 'CONFLICT',
  [HttpStatus.TOO_MANY_REQUESTS]: 'RATE_LIMITED',
};

export function errorCodeForStatus(status: number): ErrorCode {
  return statusCodeMap[status as HttpStatus] ?? 'INTERNAL_ERROR';
}

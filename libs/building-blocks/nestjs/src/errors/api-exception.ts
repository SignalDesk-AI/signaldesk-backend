import { HttpException, HttpStatus } from '@nestjs/common';

import { ErrorCode } from './error-envelope';

export class ApiException extends HttpException {
  constructor(
    public readonly code: ErrorCode | string,
    message: string,
    status: HttpStatus,
    public readonly details: unknown[] = []
  ) {
    super(message, status);
  }
}

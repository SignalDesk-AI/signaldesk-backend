import {
  ArgumentsHost,
  Catch,
  ExceptionFilter,
  HttpException,
  HttpStatus,
} from '@nestjs/common';
import { CORRELATION_ID_HEADER } from '../correlation';
import { ApiException } from './api-exception';
import { errorCodeForStatus } from './error-code';
import { createErrorEnvelope } from './error-envelope';

interface HttpExceptionResponse {
  message?: string | string[];
  error?: string;
  details?: unknown[];
}

interface ErrorEnvelopeResponse {
  status(statusCode: number): {
    json(body: unknown): void;
  };
}

@Catch()
export class ErrorEnvelopeFilter implements ExceptionFilter {
  catch(exception: unknown, host: ArgumentsHost): void {
    const http = host.switchToHttp();
    const response = http.getResponse<ErrorEnvelopeResponse>();
    const request = http.getRequest<{ headers?: Record<string, string | string[] | undefined> }>();
    const status = exception instanceof HttpException
      ? exception.getStatus()
      : HttpStatus.INTERNAL_SERVER_ERROR;
    const exceptionResponse = exception instanceof HttpException
      ? exception.getResponse()
      : undefined;
    const body = typeof exceptionResponse === 'object' && exceptionResponse !== null
      ? (exceptionResponse as HttpExceptionResponse)
      : undefined;
    const message = this.resolveMessage(exception, body, status);
    const details = exception instanceof ApiException
      ? exception.details
      : Array.isArray(body?.details)
        ? body.details
        : Array.isArray(body?.message)
          ? body.message
          : [];
    const code = exception instanceof ApiException
      ? exception.code
      : errorCodeForStatus(status);
    const correlationId = firstHeaderValue(request.headers?.[CORRELATION_ID_HEADER]);

    response.status(status).json(
      createErrorEnvelope({
        code,
        message,
        details,
        correlationId,
      })
    );
  }

  private resolveMessage(
    exception: unknown,
    body: HttpExceptionResponse | undefined,
    status: number
  ): string {
    if (exception instanceof ApiException) {
      return exception.message;
    }

    if (typeof body?.message === 'string') {
      return body.message;
    }

    if (Array.isArray(body?.message)) {
      return 'Request is invalid';
    }

    if (body?.error) {
      return body.error;
    }

    if (exception instanceof Error && status !== HttpStatus.INTERNAL_SERVER_ERROR) {
      return exception.message;
    }

    return status === HttpStatus.INTERNAL_SERVER_ERROR
      ? 'Unexpected server error'
      : 'Request failed';
  }
}

function firstHeaderValue(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

export interface IntegrationEvent<TPayload = unknown> {
  eventId: string;
  eventType: string;
  tenantId?: string;
  correlationId?: string;
  occurredAt: string;
  payload: TPayload;
}

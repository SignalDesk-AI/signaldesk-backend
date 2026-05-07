CREATE TABLE IF NOT EXISTS ops.outbox_events (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  service_name varchar(100) NOT NULL,
  event_type varchar(200) NOT NULL,
  event_version int NOT NULL DEFAULT 1,
  exchange varchar(200) NOT NULL DEFAULT 'signaldesk.domain-events',
  routing_key varchar(200) NOT NULL,
  tenant_id uuid NULL,
  aggregate_type varchar(100) NULL,
  aggregate_id varchar(100) NULL,
  aggregate_version bigint NULL,
  correlation_id varchar(100) NULL,
  causation_id uuid NULL,
  actor_id uuid NULL,
  payload jsonb NOT NULL,
  headers jsonb NOT NULL DEFAULT '{}'::jsonb,
  status varchar(30) NOT NULL DEFAULT 'pending',
  retry_count int NOT NULL DEFAULT 0,
  max_retries int NOT NULL DEFAULT 10,
  next_retry_at timestamptz NOT NULL DEFAULT now(),
  claimed_at timestamptz NULL,
  claimed_by varchar(100) NULL,
  published_at timestamptz NULL,
  last_error text NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_outbox_claim
ON ops.outbox_events(service_name, status, next_retry_at, claimed_at);

CREATE TABLE IF NOT EXISTS ops.inbox_messages (
  service_name varchar(100) NOT NULL,
  message_id uuid NOT NULL,
  event_type varchar(200) NOT NULL,
  status varchar(30) NOT NULL DEFAULT 'processing',
  payload jsonb NOT NULL,
  received_at timestamptz NOT NULL DEFAULT now(),
  processed_at timestamptz NULL,
  expires_at timestamptz NOT NULL DEFAULT (now() + interval '7 days'),
  retry_count int NOT NULL DEFAULT 0,
  last_error text NULL,
  PRIMARY KEY (service_name, message_id)
);

CREATE INDEX IF NOT EXISTS ix_inbox_processing_stale
ON ops.inbox_messages(service_name, status, received_at)
WHERE status = 'processing';

CREATE INDEX IF NOT EXISTS ix_inbox_retention
ON ops.inbox_messages(expires_at);

CREATE INDEX IF NOT EXISTS ix_inbox_processed_lookup
ON ops.inbox_messages(service_name, processed_at)
WHERE status = 'processed';

CREATE TABLE IF NOT EXISTS ops.idempotency_keys (
  tenant_id uuid NOT NULL,
  scope varchar(100) NOT NULL,
  key varchar(200) NOT NULL,
  request_hash varchar(128) NOT NULL,
  status varchar(30) NOT NULL,
  response_code int NULL,
  response_body jsonb NULL,
  expires_at timestamptz NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id, scope, key)
);

CREATE INDEX IF NOT EXISTS ix_idempotency_expires_at
ON ops.idempotency_keys(expires_at);

CREATE TABLE IF NOT EXISTS ops.audit_logs (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id uuid NULL,
  actor_id uuid NULL,
  action varchar(200) NOT NULL,
  aggregate_type varchar(100) NULL,
  aggregate_id varchar(100) NULL,
  correlation_id varchar(100) NULL,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ops.dead_letters (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source varchar(100) NOT NULL,
  service_name varchar(100) NULL,
  message_id uuid NULL,
  routing_key varchar(200) NULL,
  event_type varchar(200) NULL,
  payload jsonb NULL,
  headers jsonb NULL,
  error text NOT NULL,
  failed_at timestamptz NOT NULL DEFAULT now(),
  replay_status varchar(30) NOT NULL DEFAULT 'pending'
);

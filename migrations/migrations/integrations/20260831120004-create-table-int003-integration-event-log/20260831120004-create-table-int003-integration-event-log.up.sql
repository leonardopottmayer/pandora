-- 20260831120004-create-table-int003-integration-event-log.up.sql

-- Append-only history of a connection's health: connects, reconnects, refresh failures, expiries,
-- revocations and disconnects. The answer to "why did sync stop three days ago". Rows are written
-- and read, never mutated. No FK to int001: a disconnect deletes the account but must keep its log.
CREATE TABLE integrations.int003_integration_event_log (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	external_account_id uuid NULL,
	provider VARCHAR(40) NOT NULL,
	event_type VARCHAR(30) NOT NULL,
	detail TEXT NULL,
	occurred_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE integrations.int003_integration_event_log
ADD CONSTRAINT pk_int003 PRIMARY KEY (id);

ALTER TABLE integrations.int003_integration_event_log
ADD CONSTRAINT chk_int003_event_type
CHECK (event_type IN ('connected', 'reconnected', 'refresh_failed', 'expired', 'revoked', 'disconnected'));

-- The read path: a user's timeline, newest first.
CREATE INDEX ix_int003_user_occurred
ON integrations.int003_integration_event_log (user_id, occurred_at DESC);

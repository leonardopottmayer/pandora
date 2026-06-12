-- 20260604120001-create-table-not001-notification.up.sql

CREATE TABLE notifications.not001_notification (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	channel VARCHAR(20) NOT NULL,
	recipient VARCHAR(255) NOT NULL,
	template_key VARCHAR(100) NOT NULL,
	locale VARCHAR(10) NOT NULL,
	payload JSONB NOT NULL DEFAULT '{}'::jsonb,
	subject VARCHAR(255) NOT NULL,
	body TEXT NOT NULL,
	is_html BOOLEAN NOT NULL DEFAULT false,
	status VARCHAR(20) NOT NULL,
	attempt_count INT NOT NULL DEFAULT 0,
	max_attempts INT NOT NULL DEFAULT 5,
	next_attempt_at TIMESTAMPTZ NOT NULL,
	last_error TEXT NULL,
	provider VARCHAR(100) NULL,
	provider_message_id VARCHAR(255) NULL,
	correlation_id uuid NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE notifications.not001_notification
ADD CONSTRAINT pk_not001 PRIMARY KEY (id);

ALTER TABLE notifications.not001_notification
ADD CONSTRAINT uq_not001_correlation_id UNIQUE (correlation_id);

ALTER TABLE notifications.not001_notification
ADD CONSTRAINT chk_not001_status
CHECK (status IN ('Pending', 'Sending', 'Sent', 'Failed', 'Dead'));

CREATE INDEX ix_not001_status_next_attempt_at
ON notifications.not001_notification (status, next_attempt_at);

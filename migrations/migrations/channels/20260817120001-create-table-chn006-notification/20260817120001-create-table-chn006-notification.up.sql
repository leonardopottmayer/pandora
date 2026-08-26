-- 20260817120001-create-table-chn006-notification.up.sql

CREATE TABLE channels.chn006_notification (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	channel VARCHAR(20) NOT NULL,
	recipient VARCHAR(255) NOT NULL,
	-- The user this notification is for, and its delivery category -- both drive the history read.
	-- Nullable: an ad-hoc SendNotificationRequested send is addressed but not attributed.
	user_id uuid NULL,
	category VARCHAR(100) NULL,
	template_key VARCHAR(100) NOT NULL,
	locale VARCHAR(10) NOT NULL,
	payload JSONB NOT NULL DEFAULT '{}'::jsonb,
	subject VARCHAR(255) NOT NULL,
	body TEXT NOT NULL,
	is_html BOOLEAN NOT NULL DEFAULT false,
	-- Structured, already-rendered content for channels that e-mail's subject/body/is_html cannot
	-- express -- today, a Telegram inline keyboard. Null for e-mail, which keeps using the columns it
	-- always had.
	rendered_payload JSONB NULL,
	status VARCHAR(20) NOT NULL,
	attempt_count INT NOT NULL DEFAULT 0,
	max_attempts INT NOT NULL DEFAULT 5,
	next_attempt_at TIMESTAMPTZ NOT NULL,
	last_error TEXT NULL,
	provider VARCHAR(100) NULL,
	provider_message_id VARCHAR(255) NULL,
	correlation_id uuid NOT NULL,
	-- Common to the N rows one request fans out into (e-mail + Telegram), so they read as one
	-- notification with independent retry and status.
	group_id uuid NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn006_notification
ADD CONSTRAINT pk_chn006 PRIMARY KEY (id);

-- Dedup is per channel: a fan-out to e-mail and Telegram shares one correlation id, so uniqueness
-- must include the channel or the second row is rejected as a duplicate.
CREATE UNIQUE INDEX uq_chn006_correlation_channel
ON channels.chn006_notification (correlation_id, channel);

ALTER TABLE channels.chn006_notification
ADD CONSTRAINT chk_chn006_status
CHECK (status IN ('Pending', 'Sending', 'Sent', 'Failed', 'Dead'));

CREATE INDEX ix_chn006_status_next_attempt_at
ON channels.chn006_notification (status, next_attempt_at);

-- The delivery-history read: a user's notifications, newest first.
CREATE INDEX ix_chn006_user_created_at
ON channels.chn006_notification (user_id, created_at DESC);

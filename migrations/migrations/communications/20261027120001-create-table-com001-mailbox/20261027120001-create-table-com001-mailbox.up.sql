-- 20261027120001-create-table-com001-mailbox.up.sql

-- One connected mailbox. The credential lives in Integrations: external_account_id references
-- integrations.int001_external_account only logically (no FK leaves the schema). The imap/smtp
-- columns are set only for titan (preset) and imap; the connection is secured by port (993/465 TLS,
-- 143/587 STARTTLS). sync_cursor and backfill_cursor are opaque to everything but the provider's
-- adapter: Gmail historyId / pageToken; IMAP, per folder, UIDVALIDITY, UIDNEXT and MODSEQ / the
-- lowest UID fetched.
-- message_total_estimate is the provider's count at backfill start, for the progress bar.
CREATE TABLE communications.com001_mailbox (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	provider VARCHAR(20) NOT NULL,
	external_account_id uuid NOT NULL,
	address VARCHAR(320) NOT NULL,
	display_name VARCHAR(100) NULL,
	imap_host VARCHAR(255) NULL,
	imap_port INTEGER NULL,
	smtp_host VARCHAR(255) NULL,
	smtp_port INTEGER NULL,
	status VARCHAR(20) NOT NULL,
	sync_cursor TEXT NULL,
	backfill_cursor TEXT NULL,
	backfill_completed_at TIMESTAMPTZ NULL,
	message_total_estimate INTEGER NULL,
	last_synced_at TIMESTAMPTZ NULL,
	last_error TEXT NULL,
	consecutive_failures INTEGER NOT NULL DEFAULT 0,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE communications.com001_mailbox
ADD CONSTRAINT pk_com001 PRIMARY KEY (id);

-- One mailbox per credential: a Google account is one Gmail, a password account is one login.
ALTER TABLE communications.com001_mailbox
ADD CONSTRAINT uq_com001_user_external_account UNIQUE (user_id, external_account_id);

ALTER TABLE communications.com001_mailbox
ADD CONSTRAINT chk_com001_provider
CHECK (provider IN ('gmail', 'titan', 'imap'));

ALTER TABLE communications.com001_mailbox
ADD CONSTRAINT chk_com001_status
CHECK (status IN ('active', 'syncing', 'needs-reconnect', 'error'));

CREATE INDEX ix_com001_user_id ON communications.com001_mailbox (user_id);

-- A revoked or disconnected credential → its mailboxes.
CREATE INDEX ix_com001_external_account_id ON communications.com001_mailbox (external_account_id);

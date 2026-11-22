-- 20261109120000-create-table-agd009-calendar-binding.up.sql

-- One Pandora calendar bound to one Google calendar, on one Integrations account. The sync cursor
-- (Google's syncToken) lives here. direction: read (Google -> Pandora only), read-write (Pandora's edits go to Google too).
-- status: ok, needs-reconnect (waits for the user), error (stopped after repeated failures).
CREATE TABLE agenda.agd009_calendar_binding (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	calendar_id uuid NOT NULL,
	external_account_id uuid NOT NULL,
	remote_calendar_id VARCHAR(255) NOT NULL,
	direction VARCHAR(20) NOT NULL DEFAULT 'read',
	sync_token TEXT NULL,
	full_synced_at TIMESTAMPTZ NULL,
	last_synced_at TIMESTAMPTZ NULL,
	status VARCHAR(20) NOT NULL DEFAULT 'ok',
	failure_count INTEGER NOT NULL DEFAULT 0,
	last_error VARCHAR(1000) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL,
	CONSTRAINT chk_agd009_direction CHECK (direction IN ('read', 'read-write')),
	CONSTRAINT chk_agd009_status CHECK (status IN ('ok', 'needs-reconnect', 'error'))
);

ALTER TABLE agenda.agd009_calendar_binding
ADD CONSTRAINT pk_agd009 PRIMARY KEY (id);

ALTER TABLE agenda.agd009_calendar_binding
ADD CONSTRAINT fk_agd009_calendar
FOREIGN KEY (calendar_id) REFERENCES agenda.agd001_calendar (id);

CREATE UNIQUE INDEX uq_agd009_calendar
ON agenda.agd009_calendar_binding (calendar_id);

CREATE UNIQUE INDEX uq_agd009_account_remote
ON agenda.agd009_calendar_binding (external_account_id, remote_calendar_id);

CREATE INDEX ix_agd009_user_id
ON agenda.agd009_calendar_binding (user_id);

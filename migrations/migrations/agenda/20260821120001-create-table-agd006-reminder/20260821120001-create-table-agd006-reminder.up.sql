-- 20260821120001-create-table-agd006-reminder.up.sql

-- A ping at an instant. It fires once, then is acknowledged, snoozed or cancelled. The status also
-- serves as the sweep's idempotency guard: once 'Notified', the row is no longer selected, so a
-- restart never re-fires it. Alert/AlertDispatch tables arrive with recurrence and events, which
-- need a per-occurrence ledger this single-shot reminder does not.
CREATE TABLE agenda.agd006_reminder (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	title VARCHAR(200) NOT NULL,
	notes TEXT NULL,
	remind_at TIMESTAMPTZ NOT NULL,
	time_zone VARCHAR(100) NOT NULL DEFAULT 'UTC',
	status VARCHAR(20) NOT NULL,
	snoozed_until TIMESTAMPTZ NULL,
	acknowledged_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd006_reminder
ADD CONSTRAINT pk_agd006 PRIMARY KEY (id);

ALTER TABLE agenda.agd006_reminder
ADD CONSTRAINT chk_agd006_status
CHECK (status IN ('Scheduled', 'Notified', 'Acknowledged', 'Snoozed', 'Cancelled'));

CREATE INDEX ix_agd006_user_id
ON agenda.agd006_reminder (user_id);

-- The sweep's hot path: due rows by status and effective time.
CREATE INDEX ix_agd006_status_remind_at
ON agenda.agd006_reminder (status, remind_at);

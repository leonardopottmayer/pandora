-- 20260821120001-create-table-agd006-reminder.up.sql

-- A ping at an instant. A single-shot reminder (rrule NULL) fires once, then is acknowledged,
-- snoozed or cancelled, and its status is the sweep's idempotency guard: once 'Notified' the row is
-- no longer selected, so a restart never re-fires it. A recurring reminder (rrule set) fires once per
-- occurrence; there the guard is not the status but the per-occurrence dispatch ledger
-- (agd006x_reminder_dispatch), and the reminder's status stays 'Scheduled' for the life of the series
-- (until cancelled, which stops it).
CREATE TABLE agenda.agd006_reminder (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	title VARCHAR(200) NOT NULL,
	notes TEXT NULL,
	remind_at TIMESTAMPTZ NOT NULL,
	time_zone VARCHAR(100) NOT NULL DEFAULT 'UTC',
	-- Raw RRULE (RFC 5545 subset), stored verbatim so a Google-Calendar sync is a copy, not a lossy
	-- translation. NULL ⇒ single-shot.
	rrule TEXT NULL,
	-- Denormalized last-occurrence bound (from UNTIL/COUNT) so the sweep can prune finished series by
	-- index instead of expanding every recurring row. NULL ⇒ open-ended (or single-shot).
	recurrence_ends_at TIMESTAMPTZ NULL,
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

-- The single-shot sweep's hot path: due rows by status and effective time.
CREATE INDEX ix_agd006_status_remind_at
ON agenda.agd006_reminder (status, remind_at);

-- The recurring sweep's hot path: only recurring rows, pruned by their denormalized series end.
CREATE INDEX ix_agd006_recurrence_ends_at
ON agenda.agd006_reminder (recurrence_ends_at)
WHERE rrule IS NOT NULL;

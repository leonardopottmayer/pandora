-- 20260821120008-create-table-agd002-event.up.sql

-- A calendar event (doc agd002). Unlike a recurring task (materialized one row at a time), an event is
-- calculated, never stored: one row plus an rrule, expanded into occurrences on read. Per-occurrence
-- deviations live in agd003_event_occurrence_override.
--
-- starts_at/ends_at are timestamptz; for an all-day event they are midnight in time_zone (end
-- exclusive). recurrence_ends_at is the denormalized last-occurrence bound (from UNTIL/COUNT) so a range
-- query prunes finished series by index instead of expanding every row. status is stored PascalCase to
-- match agd006_reminder.status. deleted_at is a soft delete (a future inbound sync can resurrect).
CREATE TABLE agenda.agd002_event (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	calendar_id uuid NOT NULL,
	title VARCHAR(200) NOT NULL,
	description TEXT NULL,
	location TEXT NULL,
	-- The meeting link, if any.
	url TEXT NULL,
	starts_at TIMESTAMPTZ NOT NULL,
	ends_at TIMESTAMPTZ NOT NULL,
	is_all_day BOOLEAN NOT NULL DEFAULT false,
	-- IANA, per event — recurrence is expanded in this zone.
	time_zone VARCHAR(100) NOT NULL DEFAULT 'UTC',
	-- Raw RRULE (RFC 5545 subset), stored verbatim. NULL ⇒ a single occurrence.
	rrule TEXT NULL,
	recurrence_ends_at TIMESTAMPTZ NULL,
	status VARCHAR(20) NOT NULL DEFAULT 'Confirmed',
	deleted_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd002_event
ADD CONSTRAINT pk_agd002 PRIMARY KEY (id);

-- A calendar keeps its events: deleting a non-empty calendar is refused (the app archives instead).
ALTER TABLE agenda.agd002_event
ADD CONSTRAINT fk_agd002_calendar
FOREIGN KEY (calendar_id) REFERENCES agenda.agd001_calendar (id) ON DELETE RESTRICT;

ALTER TABLE agenda.agd002_event
ADD CONSTRAINT chk_agd002_status
CHECK (status IN ('Confirmed', 'Tentative', 'Cancelled'));

CREATE INDEX ix_agd002_user_id
ON agenda.agd002_event (user_id);

-- The range query and the delete guard both scan by calendar.
CREATE INDEX ix_agd002_calendar_id
ON agenda.agd002_event (calendar_id);

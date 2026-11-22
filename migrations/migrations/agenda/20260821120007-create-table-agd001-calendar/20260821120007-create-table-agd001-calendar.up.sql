-- 20260821120007-create-table-agd001-calendar.up.sql

-- A named container of events (doc agd001), the calendar-grid counterpart of agd004_task_list. Exactly
-- one calendar per user is the default, guarded by a partial unique index. Archiving hides it without
-- deleting; deleting a calendar with live events is refused by the application (archive instead).
--
-- time_zone is carried on the row because recurrence expands in the calendar/event's own zone and the
-- Identity time-zone is deferred. origin is 'local' | 'external'; only 'local' matters until Google
-- sync (Phase 5). Enum-like columns are stored PascalCase to match agd006_reminder.status.
CREATE TABLE agenda.agd001_calendar (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(200) NOT NULL,
	color VARCHAR(50) NULL,
	is_default BOOLEAN NOT NULL DEFAULT false,
	is_visible BOOLEAN NOT NULL DEFAULT true,
	time_zone VARCHAR(100) NOT NULL DEFAULT 'UTC',
	origin VARCHAR(20) NOT NULL DEFAULT 'Local',
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd001_calendar
ADD CONSTRAINT pk_agd001 PRIMARY KEY (id);

ALTER TABLE agenda.agd001_calendar
ADD CONSTRAINT chk_agd001_origin
CHECK (origin IN ('Local', 'External'));

CREATE INDEX ix_agd001_user_id
ON agenda.agd001_calendar (user_id);

-- At most one default calendar per user.
CREATE UNIQUE INDEX uq_agd001_user_default
ON agenda.agd001_calendar (user_id)
WHERE is_default;

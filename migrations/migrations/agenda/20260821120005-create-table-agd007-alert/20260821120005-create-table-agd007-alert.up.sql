-- 20260821120005-create-table-agd007-alert.up.sql

-- "Notify me about <subject> with <this offset>, on <these channels>." The polymorphic scheduling
-- primitive (doc agd007): one row per wanted ping, keyed to a subject by (subject_type, subject_id)
-- with no FK — validated in the application, removed with the subject.
--
-- The column is polymorphic by design, so the CHECK admits all three subject types; Phase 3 only
-- wires 'Task'. Events arrive with the calendar (Phase 4); reminders keep their own agd006x ledger for
-- now and migrate here later. Subject types are stored PascalCase to match agd006_reminder.status.
CREATE TABLE agenda.agd007_alert (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	subject_type VARCHAR(20) NOT NULL,
	subject_id uuid NOT NULL,
	-- Signed, relative to the subject anchor (a task's due_at). 0 = at the instant, -15 = fifteen
	-- minutes before.
	offset_minutes INTEGER NOT NULL,
	-- NULL ⇒ resolve from the user's preference for the category in Channels. Otherwise the explicit
	-- channels ('email', 'telegram').
	channels TEXT[] NULL,
	is_enabled BOOLEAN NOT NULL DEFAULT true,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd007_alert
ADD CONSTRAINT pk_agd007 PRIMARY KEY (id);

ALTER TABLE agenda.agd007_alert
ADD CONSTRAINT chk_agd007_subject_type
CHECK (subject_type IN ('Task', 'Event', 'Reminder'));

CREATE INDEX ix_agd007_user_id
ON agenda.agd007_alert (user_id);

-- Resolving a subject's alerts (listing, and carrying them to a recurring task's next instance).
CREATE INDEX ix_agd007_subject
ON agenda.agd007_alert (subject_type, subject_id);

-- The sweep's scan root: enabled alerts of a subject type.
CREATE INDEX ix_agd007_enabled_subject_type
ON agenda.agd007_alert (subject_type)
WHERE is_enabled;

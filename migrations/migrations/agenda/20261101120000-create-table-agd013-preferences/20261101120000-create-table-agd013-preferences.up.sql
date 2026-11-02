-- 20261101120000-create-table-agd013-preferences.up.sql

-- What a user prefers about the Agenda itself, one row per user. No row means the defaults.
-- default_alert_offset_minutes is signed, relative to the item (-15 = fifteen minutes before).
CREATE TABLE agenda.agd013_preferences (
	user_id uuid NOT NULL,
	default_alert_offset_minutes INTEGER NOT NULL DEFAULT -15,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd013_preferences
ADD CONSTRAINT pk_agd013 PRIMARY KEY (user_id);

-- 20260821120009-create-table-agd003-event-occurrence-override.up.sql

-- A per-occurrence deviation from an event series (doc agd003). Its natural key is
-- (event_id, original_starts_at) — which occurrence, identified by its on-grid start. is_cancelled is
-- the EXDATE case (the occurrence disappears); otherwise the non-null columns override the series for
-- that one occurrence. Editing "this and future" instead splits the series (a new agd002 row), so it
-- writes no override here.
CREATE TABLE agenda.agd003_event_occurrence_override (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	event_id uuid NOT NULL,
	user_id uuid NOT NULL,
	original_starts_at TIMESTAMPTZ NOT NULL,
	is_cancelled BOOLEAN NOT NULL DEFAULT false,
	-- NULL columns fall back to the series value on read.
	starts_at TIMESTAMPTZ NULL,
	ends_at TIMESTAMPTZ NULL,
	title VARCHAR(200) NULL,
	description TEXT NULL,
	location TEXT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd003_event_occurrence_override
ADD CONSTRAINT pk_agd003 PRIMARY KEY (id);

-- Deleting an event removes its overrides.
ALTER TABLE agenda.agd003_event_occurrence_override
ADD CONSTRAINT fk_agd003_event
FOREIGN KEY (event_id) REFERENCES agenda.agd002_event (id) ON DELETE CASCADE;

-- One override per (event, occurrence).
ALTER TABLE agenda.agd003_event_occurrence_override
ADD CONSTRAINT uq_agd003_event_occurrence UNIQUE (event_id, original_starts_at);

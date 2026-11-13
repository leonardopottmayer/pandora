-- 20261113120000-create-table-agd011-sync-conflict.up.sql

-- An event both Pandora and Google changed between two syncs. The newer edit won; the losing version is
-- kept here as JSON so the user can see what was overwritten. losing_side: pandora or google.
-- A log: no foreign keys, so it outlives an unlinked calendar.
CREATE TABLE agenda.agd011_sync_conflict (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	binding_id uuid NOT NULL,
	event_id uuid NOT NULL,
	title VARCHAR(200) NOT NULL,
	losing_side VARCHAR(20) NOT NULL,
	losing_version JSONB NOT NULL,
	detected_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	CONSTRAINT chk_agd011_losing_side CHECK (losing_side IN ('pandora', 'google'))
);

ALTER TABLE agenda.agd011_sync_conflict
ADD CONSTRAINT pk_agd011 PRIMARY KEY (id);

CREATE INDEX ix_agd011_user_detected
ON agenda.agd011_sync_conflict (user_id, detected_at DESC);

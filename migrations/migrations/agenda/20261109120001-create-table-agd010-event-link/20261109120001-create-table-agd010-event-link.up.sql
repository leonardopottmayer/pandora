-- 20261109120001-create-table-agd010-event-link.up.sql

-- One Pandora event linked to one Google event of a binding: what keeps a sync from duplicating events.
-- series_remote_id is set on the single events made from the instances of a Google series Pandora's
-- recurrence engine cannot read (BYSETPOS, RDATE...): it names that series.
-- reminders_hash tells whether the event's Google reminders changed since its alerts were written.
-- pending_push_at: when Pandora last changed the event and has not sent it to Google yet (5b).
CREATE TABLE agenda.agd010_event_link (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	binding_id uuid NOT NULL,
	event_id uuid NOT NULL,
	remote_event_id VARCHAR(1024) NOT NULL,
	series_remote_id VARCHAR(1024) NULL,
	etag VARCHAR(255) NULL,
	remote_updated_at TIMESTAMPTZ NULL,
	reminders_hash VARCHAR(64) NULL,
	pending_push_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE agenda.agd010_event_link
ADD CONSTRAINT pk_agd010 PRIMARY KEY (id);

ALTER TABLE agenda.agd010_event_link
ADD CONSTRAINT fk_agd010_binding
FOREIGN KEY (binding_id) REFERENCES agenda.agd009_calendar_binding (id);

ALTER TABLE agenda.agd010_event_link
ADD CONSTRAINT fk_agd010_event
FOREIGN KEY (event_id) REFERENCES agenda.agd002_event (id);

CREATE UNIQUE INDEX uq_agd010_binding_remote
ON agenda.agd010_event_link (binding_id, remote_event_id);

CREATE UNIQUE INDEX uq_agd010_event
ON agenda.agd010_event_link (event_id);

CREATE INDEX ix_agd010_binding_series
ON agenda.agd010_event_link (binding_id, series_remote_id)
WHERE series_remote_id IS NOT NULL;

CREATE INDEX ix_agd010_binding_pending
ON agenda.agd010_event_link (binding_id)
WHERE pending_push_at IS NOT NULL;

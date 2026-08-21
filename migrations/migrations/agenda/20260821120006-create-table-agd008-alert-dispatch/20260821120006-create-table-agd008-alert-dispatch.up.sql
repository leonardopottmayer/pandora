-- 20260821120006-create-table-agd008-alert-dispatch.up.sql

-- The alert dispatch ledger (doc agd008). One row the first time an alert fires for a subject anchor;
-- the UNIQUE (alert_id, occurrence_starts_at) is the idempotency key, so re-running the sweep over the
-- same tick — or restarting mid-tick — never double-fires and never skips.
--
-- Unlike the reminder ledger (agd006x), this one carries no acknowledge/snooze: a task alert's button
-- completes the task itself, and task alerts have no per-occurrence snooze.
CREATE TABLE agenda.agd008_alert_dispatch (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	alert_id uuid NOT NULL,
	user_id uuid NOT NULL,
	occurrence_starts_at TIMESTAMPTZ NOT NULL,
	dispatched_at TIMESTAMPTZ NOT NULL,
	correlation_id uuid NOT NULL,
	-- Fired from the grace window rather than on its tick (a suspended machine caught up). Informational.
	is_late BOOLEAN NOT NULL DEFAULT false,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd008_alert_dispatch
ADD CONSTRAINT pk_agd008 PRIMARY KEY (id);

ALTER TABLE agenda.agd008_alert_dispatch
ADD CONSTRAINT fk_agd008_alert
FOREIGN KEY (alert_id) REFERENCES agenda.agd007_alert (id) ON DELETE CASCADE;

-- The idempotency key: one dispatch per (alert, occurrence).
ALTER TABLE agenda.agd008_alert_dispatch
ADD CONSTRAINT uq_agd008_alert_occurrence UNIQUE (alert_id, occurrence_starts_at);

CREATE INDEX ix_agd008_alert_id
ON agenda.agd008_alert_dispatch (alert_id);

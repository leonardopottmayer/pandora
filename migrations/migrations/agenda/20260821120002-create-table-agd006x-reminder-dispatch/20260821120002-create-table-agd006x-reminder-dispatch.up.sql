-- 20260821120002-create-table-agd006x-reminder-dispatch.up.sql

-- Per-occurrence dispatch ledger for reminders. It is what makes the sweep idempotent for a recurring
-- reminder, which the status column cannot guard because such a reminder fires many times. One row is
-- written the first time an occurrence is dispatched; the UNIQUE (reminder_id, occurrence_starts_at)
-- is the idempotency key, so re-running the sweep over the same tick — or restarting mid-tick — never
-- double-fires and never skips.
--
-- Named as an extension of the reminder aggregate (agd006x_), not the polymorphic agd008_alert_dispatch
-- of the plan: the Alert aggregate arrives with events/tasks in a later phase, and until it exists a
-- reminder-scoped ledger is the honest shape. It migrates to agd008 when Alert lands.
--
-- acknowledged_at / snoozed_until carry the per-occurrence action for a recurring reminder: ack and
-- snooze act on the occurrence, never on the series. A snoozed occurrence re-fires once when
-- snoozed_until passes (the sweep clears it on re-fire); an acknowledged one never re-fires.
CREATE TABLE agenda.agd006x_reminder_dispatch (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	reminder_id uuid NOT NULL,
	user_id uuid NOT NULL,
	occurrence_starts_at TIMESTAMPTZ NOT NULL,
	dispatched_at TIMESTAMPTZ NOT NULL,
	correlation_id uuid NOT NULL,
	-- Fired from the grace window rather than on its tick (a suspended machine caught up). Informational.
	is_late BOOLEAN NOT NULL DEFAULT false,
	acknowledged_at TIMESTAMPTZ NULL,
	snoozed_until TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd006x_reminder_dispatch
ADD CONSTRAINT pk_agd006x PRIMARY KEY (id);

ALTER TABLE agenda.agd006x_reminder_dispatch
ADD CONSTRAINT fk_agd006x_reminder
FOREIGN KEY (reminder_id) REFERENCES agenda.agd006_reminder (id) ON DELETE CASCADE;

-- The idempotency key: one dispatch per (reminder, occurrence).
ALTER TABLE agenda.agd006x_reminder_dispatch
ADD CONSTRAINT uq_agd006x_reminder_occurrence UNIQUE (reminder_id, occurrence_starts_at);

CREATE INDEX ix_agd006x_reminder_id
ON agenda.agd006x_reminder_dispatch (reminder_id);

-- The snooze re-fire path: occurrences waiting to fire again.
CREATE INDEX ix_agd006x_snoozed_until
ON agenda.agd006x_reminder_dispatch (snoozed_until)
WHERE snoozed_until IS NOT NULL;

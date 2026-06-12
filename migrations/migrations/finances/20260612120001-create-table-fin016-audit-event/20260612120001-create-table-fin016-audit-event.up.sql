-- 20260612120001-create-table-fin016-audit-event.up.sql

CREATE TABLE finances.fin016_audit_event (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	actor_user_id uuid NULL,
	entity_type VARCHAR(40) NOT NULL,
	entity_id uuid NOT NULL,
	event_type VARCHAR(60) NOT NULL,
	data JSONB NULL,
	correlation_id uuid NULL,
	occurred_at TIMESTAMPTZ NOT NULL
);

ALTER TABLE finances.fin016_audit_event
ADD CONSTRAINT pk_fin016 PRIMARY KEY (id);

CREATE INDEX ix_fin016_entity
ON finances.fin016_audit_event (entity_type, entity_id, occurred_at);

CREATE INDEX ix_fin016_user_occurred_at
ON finances.fin016_audit_event (user_id, occurred_at);

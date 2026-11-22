-- 20261106120006-create-table-adm006-audit-entry.up.sql

-- Every change made to flags, roles and grants, with the state before and after. actor_id is NULL
-- for changes the system makes itself (the master admin bootstrap).
CREATE TABLE admin.adm006_audit_entry (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	actor_id uuid NULL,
	action VARCHAR(50) NOT NULL,
	target_type VARCHAR(30) NOT NULL,
	target_id VARCHAR(150) NOT NULL,
	before jsonb NULL,
	after jsonb NULL,
	occurred_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE admin.adm006_audit_entry
ADD CONSTRAINT pk_adm006 PRIMARY KEY (id);

CREATE INDEX ix_adm006_occurred_at ON admin.adm006_audit_entry (occurred_at DESC);

CREATE INDEX ix_adm006_actor_id ON admin.adm006_audit_entry (actor_id);

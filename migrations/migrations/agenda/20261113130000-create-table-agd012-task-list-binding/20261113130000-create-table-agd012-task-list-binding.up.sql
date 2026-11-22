-- 20261113130000-create-table-agd012-task-list-binding.up.sql

-- One Pandora task list bound to one Google Tasks list, on one Integrations account. Always two-way:
-- a Google task list belongs to its account. updated_cursor is the instant the last listing started;
-- the next one asks Google for what changed since (updatedMin), deletions included.
-- status: ok, needs-reconnect (waits for the user), error (stopped after repeated failures).
CREATE TABLE agenda.agd012_task_list_binding (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	list_id uuid NOT NULL,
	external_account_id uuid NOT NULL,
	remote_list_id VARCHAR(255) NOT NULL,
	updated_cursor TIMESTAMPTZ NULL,
	full_synced_at TIMESTAMPTZ NULL,
	last_synced_at TIMESTAMPTZ NULL,
	status VARCHAR(20) NOT NULL DEFAULT 'ok',
	failure_count INTEGER NOT NULL DEFAULT 0,
	last_error VARCHAR(1000) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL,
	CONSTRAINT chk_agd012_status CHECK (status IN ('ok', 'needs-reconnect', 'error'))
);

ALTER TABLE agenda.agd012_task_list_binding
ADD CONSTRAINT pk_agd012 PRIMARY KEY (id);

ALTER TABLE agenda.agd012_task_list_binding
ADD CONSTRAINT fk_agd012_list
FOREIGN KEY (list_id) REFERENCES agenda.agd004_task_list (id);

CREATE UNIQUE INDEX uq_agd012_list
ON agenda.agd012_task_list_binding (list_id);

CREATE UNIQUE INDEX uq_agd012_account_remote
ON agenda.agd012_task_list_binding (external_account_id, remote_list_id);

CREATE INDEX ix_agd012_user_id
ON agenda.agd012_task_list_binding (user_id);

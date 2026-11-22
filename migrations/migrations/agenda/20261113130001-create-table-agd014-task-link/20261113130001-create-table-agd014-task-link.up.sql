-- 20261113130001-create-table-agd014-task-link.up.sql

-- One Pandora task linked to one Google task of a binding: what keeps a sync from duplicating tasks.
-- remote_task_id is null while a task born in Pandora waits for its first insert (Google picks the id).
-- pending_push_at: when Pandora last changed the task and has not sent it to Google yet.
CREATE TABLE agenda.agd014_task_link (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	binding_id uuid NOT NULL,
	task_id uuid NOT NULL,
	remote_task_id VARCHAR(1024) NULL,
	etag VARCHAR(255) NULL,
	remote_updated_at TIMESTAMPTZ NULL,
	pending_push_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE agenda.agd014_task_link
ADD CONSTRAINT pk_agd014 PRIMARY KEY (id);

ALTER TABLE agenda.agd014_task_link
ADD CONSTRAINT fk_agd014_binding
FOREIGN KEY (binding_id) REFERENCES agenda.agd012_task_list_binding (id);

ALTER TABLE agenda.agd014_task_link
ADD CONSTRAINT fk_agd014_task
FOREIGN KEY (task_id) REFERENCES agenda.agd005_task (id);

CREATE UNIQUE INDEX uq_agd014_binding_remote
ON agenda.agd014_task_link (binding_id, remote_task_id);

CREATE UNIQUE INDEX uq_agd014_task
ON agenda.agd014_task_link (task_id);

CREATE INDEX ix_agd014_binding_pending
ON agenda.agd014_task_link (binding_id)
WHERE pending_push_at IS NOT NULL;

-- 20260821120003-create-table-agd004-task-list.up.sql

-- A named container of tasks (the Apple Reminders "list", the Todoist "project"). Exactly one list
-- per user is the default, guarded by a partial unique index. Archiving hides it without deleting.
CREATE TABLE agenda.agd004_task_list (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(200) NOT NULL,
	is_default BOOLEAN NOT NULL DEFAULT false,
	position INTEGER NOT NULL DEFAULT 0,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd004_task_list
ADD CONSTRAINT pk_agd004 PRIMARY KEY (id);

CREATE INDEX ix_agd004_user_id
ON agenda.agd004_task_list (user_id);

-- At most one default list per user.
CREATE UNIQUE INDEX uq_agd004_user_default
ON agenda.agd004_task_list (user_id)
WHERE is_default;

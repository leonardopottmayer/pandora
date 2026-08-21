-- 20260821120004-create-table-agd005-task.up.sql

-- Something to do: status, priority, an optional due date, optional one-level subtasks and optional
-- recurrence. A recurring task is materialized one instance at a time — completing it closes the
-- current row (status 'Done', completed_at) and the application inserts the next instance from the
-- RRULE, carrying its fields and alerts. Two rows, not one mutable row, so history survives.
--
-- time_zone is carried on the row (not in the doc's agd005) because recurrence must expand in the
-- task's own zone and the Identity time-zone is deferred; it mirrors agd006_reminder. There is no
-- recurrence_ends_at: with one-at-a-time materialization there is no series to prune by index.
CREATE TABLE agenda.agd005_task (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	list_id uuid NOT NULL,
	-- Subtasks are tasks. Depth is limited to one level (guaranteed in the aggregate).
	parent_task_id uuid NULL,
	title VARCHAR(200) NOT NULL,
	notes TEXT NULL,
	-- A task "for tomorrow" does not fall due at 00:00 — due_has_time drives rendering and the default
	-- alert offset.
	due_at TIMESTAMPTZ NULL,
	due_has_time BOOLEAN NOT NULL DEFAULT false,
	priority VARCHAR(10) NOT NULL DEFAULT 'None',
	status VARCHAR(20) NOT NULL DEFAULT 'Todo',
	completed_at TIMESTAMPTZ NULL,
	time_zone VARCHAR(100) NOT NULL DEFAULT 'UTC',
	-- Raw RRULE (RFC 5545 subset), stored verbatim. NULL ⇒ not recurring. Only on top-level tasks.
	rrule TEXT NULL,
	position INTEGER NOT NULL DEFAULT 0,
	deleted_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE agenda.agd005_task
ADD CONSTRAINT pk_agd005 PRIMARY KEY (id);

-- A list keeps its tasks: deleting a non-empty list is refused (the app archives instead).
ALTER TABLE agenda.agd005_task
ADD CONSTRAINT fk_agd005_list
FOREIGN KEY (list_id) REFERENCES agenda.agd004_task_list (id) ON DELETE RESTRICT;

-- Deleting a parent removes its subtasks.
ALTER TABLE agenda.agd005_task
ADD CONSTRAINT fk_agd005_parent
FOREIGN KEY (parent_task_id) REFERENCES agenda.agd005_task (id) ON DELETE CASCADE;

ALTER TABLE agenda.agd005_task
ADD CONSTRAINT chk_agd005_priority
CHECK (priority IN ('None', 'Low', 'Medium', 'High'));

ALTER TABLE agenda.agd005_task
ADD CONSTRAINT chk_agd005_status
CHECK (status IN ('Todo', 'InProgress', 'Done', 'Cancelled'));

CREATE INDEX ix_agd005_user_id
ON agenda.agd005_task (user_id);

-- The list screen's hot path: a user's tasks by list and status.
CREATE INDEX ix_agd005_list_status
ON agenda.agd005_task (list_id, status);

CREATE INDEX ix_agd005_parent_task_id
ON agenda.agd005_task (parent_task_id)
WHERE parent_task_id IS NOT NULL;

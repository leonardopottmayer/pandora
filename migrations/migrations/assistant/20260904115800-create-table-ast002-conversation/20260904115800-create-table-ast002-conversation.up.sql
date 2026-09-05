-- 20260904115800-create-table-ast002-conversation.up.sql

-- A short-lived thread of interpretations for one user, grouping the messages and invocations that
-- belong together so a follow-up has context. A conversation lapses after 30 minutes of silence
-- (enforced in code): past that, the next utterance opens a fresh one.
CREATE TABLE assistant.ast002_conversation (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	started_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	last_activity_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE assistant.ast002_conversation
ADD CONSTRAINT pk_ast002 PRIMARY KEY (id);

-- The most recent active conversation per user is looked up on every utterance.
CREATE INDEX ix_ast002_user_activity
ON assistant.ast002_conversation (user_id, last_activity_at DESC);

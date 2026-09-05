-- 20260904120000-create-table-ast004-command-invocation.up.sql

-- The assistant's audit trail: one row per interpreted utterance. Records what the user said, the tool
-- call the model produced (command_name + arguments), and how it ended (status), plus the provider,
-- model, latency and token cost of the call. Most rows are terminal at write time; a
-- 'pending_confirmation' row awaits a confirm/cancel (until expires_at) and then transitions.
CREATE TABLE assistant.ast004_command_invocation (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	conversation_id uuid NOT NULL,
	utterance TEXT NOT NULL,
	command_name VARCHAR(100) NULL,
	arguments JSONB NULL,
	status VARCHAR(20) NOT NULL,
	result TEXT NULL,
	error TEXT NULL,
	provider VARCHAR(40) NOT NULL,
	model VARCHAR(100) NOT NULL,
	latency_ms BIGINT NOT NULL,
	prompt_tokens INTEGER NOT NULL DEFAULT 0,
	completion_tokens INTEGER NOT NULL DEFAULT 0,
	expires_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE assistant.ast004_command_invocation
ADD CONSTRAINT pk_ast004 PRIMARY KEY (id);

ALTER TABLE assistant.ast004_command_invocation
ADD CONSTRAINT fk_ast004_conversation
FOREIGN KEY (conversation_id) REFERENCES assistant.ast002_conversation (id) ON DELETE CASCADE;

ALTER TABLE assistant.ast004_command_invocation
ADD CONSTRAINT chk_ast004_status
CHECK (status IN ('executed', 'failed', 'clarification', 'rejected', 'provider-error',
                  'pending-confirmation', 'cancelled', 'expired'));

-- The audit trail is read newest-first, per user.
CREATE INDEX ix_ast004_user_created
ON assistant.ast004_command_invocation (user_id, created_at DESC);

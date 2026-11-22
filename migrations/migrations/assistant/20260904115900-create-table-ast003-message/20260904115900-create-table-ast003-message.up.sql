-- 20260904115900-create-table-ast003-message.up.sql

-- One turn stored in a conversation: the user's utterance or the assistant's reply. Kept for context
-- and audit; the model's structured tool call lives on ast004_command_invocation, not here.
CREATE TABLE assistant.ast003_message (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	conversation_id uuid NOT NULL,
	author VARCHAR(20) NOT NULL,
	content TEXT NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE assistant.ast003_message
ADD CONSTRAINT pk_ast003 PRIMARY KEY (id);

ALTER TABLE assistant.ast003_message
ADD CONSTRAINT fk_ast003_conversation
FOREIGN KEY (conversation_id) REFERENCES assistant.ast002_conversation (id) ON DELETE CASCADE;

ALTER TABLE assistant.ast003_message
ADD CONSTRAINT chk_ast003_author
CHECK (author IN ('user', 'assistant'));

-- Messages are read in order within a conversation.
CREATE INDEX ix_ast003_conversation_created
ON assistant.ast003_message (conversation_id, created_at);

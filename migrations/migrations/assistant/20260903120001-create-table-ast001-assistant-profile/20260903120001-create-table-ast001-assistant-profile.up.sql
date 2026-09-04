-- 20260903120001-create-table-ast001-assistant-profile.up.sql

-- One assistant configuration per user: which chat provider and model interpret their language,
-- whether the assistant is on, and how readily it executes without confirming. The API key itself is
-- not here — it lives in integrations.int001_external_account and is fetched per call by chat_provider.
CREATE TABLE assistant.ast001_assistant_profile (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	chat_provider VARCHAR(40) NOT NULL,
	chat_model VARCHAR(100) NOT NULL,
	is_enabled BOOLEAN NOT NULL DEFAULT false,
	locale_override VARCHAR(20) NULL,
	confirmation_level VARCHAR(20) NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE assistant.ast001_assistant_profile
ADD CONSTRAINT pk_ast001 PRIMARY KEY (id);

ALTER TABLE assistant.ast001_assistant_profile
ADD CONSTRAINT chk_ast001_confirmation_level
CHECK (confirmation_level IN ('strict', 'balanced', 'trusting'));

-- One profile per user.
CREATE UNIQUE INDEX uq_ast001_user
ON assistant.ast001_assistant_profile (user_id);

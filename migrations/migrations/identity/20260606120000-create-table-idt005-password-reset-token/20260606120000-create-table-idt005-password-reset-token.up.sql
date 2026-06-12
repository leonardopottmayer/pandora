-- 20260606120000-create-table-idt005-password-reset-token.up.sql

CREATE TABLE identity.idt005_password_reset_token (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	token_hash VARCHAR(64) NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt005_password_reset_token
ADD CONSTRAINT pk_idt005 PRIMARY KEY (id);

ALTER TABLE identity.idt005_password_reset_token
ADD CONSTRAINT uq_idt005_token_hash UNIQUE (token_hash);

ALTER TABLE identity.idt005_password_reset_token
ADD CONSTRAINT fk_idt005_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

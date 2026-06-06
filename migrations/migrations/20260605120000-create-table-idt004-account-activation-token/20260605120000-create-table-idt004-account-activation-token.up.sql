-- 20260605120000-create-table-idt004-account-activation-token.up.sql

CREATE TABLE identity.idt004_account_activation_token (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	token_hash VARCHAR(64) NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt004_account_activation_token
ADD CONSTRAINT pk_idt004 PRIMARY KEY (id);

ALTER TABLE identity.idt004_account_activation_token
ADD CONSTRAINT uq_idt004_token_hash UNIQUE (token_hash);

ALTER TABLE identity.idt004_account_activation_token
ADD CONSTRAINT fk_idt004_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

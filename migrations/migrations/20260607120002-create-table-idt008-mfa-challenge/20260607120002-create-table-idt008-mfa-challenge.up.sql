-- 20260607120002-create-table-idt008-mfa-challenge.up.sql

CREATE TABLE identity.idt008_mfa_challenge (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	token_hash VARCHAR(64) NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt008_mfa_challenge
ADD CONSTRAINT pk_idt008 PRIMARY KEY (id);

ALTER TABLE identity.idt008_mfa_challenge
ADD CONSTRAINT uq_idt008_token_hash UNIQUE (token_hash);

CREATE INDEX ix_idt008_user_id
ON identity.idt008_mfa_challenge (user_id);

ALTER TABLE identity.idt008_mfa_challenge
ADD CONSTRAINT fk_idt008_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

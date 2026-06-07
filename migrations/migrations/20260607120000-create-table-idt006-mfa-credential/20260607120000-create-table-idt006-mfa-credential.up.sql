-- 20260607120000-create-table-idt006-mfa-credential.up.sql

CREATE TABLE identity.idt006_mfa_credential (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	secret_cipher TEXT NOT NULL,
	confirmed_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE identity.idt006_mfa_credential
ADD CONSTRAINT pk_idt006 PRIMARY KEY (id);

ALTER TABLE identity.idt006_mfa_credential
ADD CONSTRAINT uq_idt006_user_id UNIQUE (user_id);

ALTER TABLE identity.idt006_mfa_credential
ADD CONSTRAINT fk_idt006_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

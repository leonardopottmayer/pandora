-- 20260607120001-create-table-idt007-mfa-recovery-code.up.sql

CREATE TABLE identity.idt007_mfa_recovery_code (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	code_hash VARCHAR(64) NOT NULL,
	consumed_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE identity.idt007_mfa_recovery_code
ADD CONSTRAINT pk_idt007 PRIMARY KEY (id);

ALTER TABLE identity.idt007_mfa_recovery_code
ADD CONSTRAINT uq_idt007_code_hash UNIQUE (code_hash);

CREATE INDEX ix_idt007_user_id
ON identity.idt007_mfa_recovery_code (user_id);

ALTER TABLE identity.idt007_mfa_recovery_code
ADD CONSTRAINT fk_idt007_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

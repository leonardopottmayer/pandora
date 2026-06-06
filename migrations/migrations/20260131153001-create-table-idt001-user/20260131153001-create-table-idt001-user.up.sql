-- 20260131153001-create-table-idt001-user.up.sql

CREATE TABLE identity.idt001_user (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	name VARCHAR(150) NOT NULL,
	username VARCHAR(50) NOT NULL,
	email VARCHAR(255) NOT NULL,
	password_hash TEXT NOT NULL,
	email_confirmed_at TIMESTAMPTZ NULL,
	disabled_at TIMESTAMPTZ NULL,
	mfa_enabled BOOLEAN NOT NULL DEFAULT false,
	last_sign_in_at TIMESTAMPTZ NULL,
	last_password_changed_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt001_user
ADD CONSTRAINT pk_idt001 PRIMARY KEY (id);

ALTER TABLE identity.idt001_user
ADD CONSTRAINT uq_idt001_username UNIQUE (username);

ALTER TABLE identity.idt001_user
ADD CONSTRAINT uq_idt001_email UNIQUE (email);

ALTER TABLE identity.idt001_user
ADD CONSTRAINT fk_idt001_created_by FOREIGN KEY (created_by) REFERENCES identity.idt001_user (id);

ALTER TABLE identity.idt001_user
ADD CONSTRAINT fk_idt001_updated_by FOREIGN KEY (updated_by) REFERENCES identity.idt001_user (id);

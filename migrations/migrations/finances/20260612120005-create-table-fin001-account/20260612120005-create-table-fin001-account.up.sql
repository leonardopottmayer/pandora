-- 20260612120005-create-table-fin001-account.up.sql

CREATE TABLE finances.fin001_account (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	type VARCHAR(20) NOT NULL,
	currency VARCHAR(10) NOT NULL,
	institution VARCHAR(100) NULL,
	description VARCHAR(255) NULL,
	color VARCHAR(20) NULL,
	icon VARCHAR(50) NULL,
	display_order INT NOT NULL DEFAULT 0,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin001_account
ADD CONSTRAINT pk_fin001 PRIMARY KEY (id);

ALTER TABLE finances.fin001_account
ADD CONSTRAINT uq_fin001_user_name UNIQUE (user_id, name);

ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_type
CHECK (type IN ('cash', 'checking', 'savings', 'international', 'crypto', 'investment', 'other'));

CREATE INDEX ix_fin001_user_id
ON finances.fin001_account (user_id);

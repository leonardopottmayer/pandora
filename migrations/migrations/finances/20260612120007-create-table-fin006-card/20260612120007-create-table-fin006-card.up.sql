-- 20260612120007-create-table-fin006-card.up.sql

CREATE TABLE finances.fin006_card (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	brand VARCHAR(50) NULL,
	last_four VARCHAR(4) NULL,
	credit_limit NUMERIC(20,8) NULL,
	closing_day INT NOT NULL,
	due_day INT NOT NULL,
	currency VARCHAR(10) NOT NULL,
	default_payment_account_id uuid NULL,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT pk_fin006 PRIMARY KEY (id);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT uq_fin006_user_name UNIQUE (user_id, name);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT ck_fin006_closing_day CHECK (closing_day BETWEEN 1 AND 28);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT ck_fin006_due_day CHECK (due_day BETWEEN 1 AND 28);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT ck_fin006_credit_limit CHECK (credit_limit IS NULL OR credit_limit >= 0);

ALTER TABLE finances.fin006_card
ADD CONSTRAINT fk_fin006_default_payment_account_id FOREIGN KEY (default_payment_account_id)
	REFERENCES finances.fin001_account (id);

CREATE INDEX ix_fin006_user_archived_at
ON finances.fin006_card (user_id, archived_at);

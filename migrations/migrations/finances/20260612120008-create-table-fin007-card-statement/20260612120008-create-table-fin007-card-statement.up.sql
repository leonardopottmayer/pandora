-- 20260612120008-create-table-fin007-card-statement.up.sql

CREATE TABLE finances.fin007_card_statement (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	card_id uuid NOT NULL,
	reference_month VARCHAR(7) NOT NULL,
	closing_date DATE NOT NULL,
	due_date DATE NOT NULL,
	status VARCHAR(20) NOT NULL DEFAULT 'open',
	total_amount NUMERIC(20,8) NOT NULL DEFAULT 0,
	paid_amount NUMERIC(20,8) NOT NULL DEFAULT 0,
	closed_at TIMESTAMPTZ NULL,
	paid_at TIMESTAMPTZ NULL,
	overdue_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT pk_fin007 PRIMARY KEY (id);

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT uq_fin007_card_reference_month UNIQUE (card_id, reference_month);

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT ck_fin007_status
CHECK (status IN ('open', 'closed', 'partially-paid', 'paid', 'overdue'));

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT ck_fin007_reference_month
CHECK (reference_month ~ '^[0-9]{4}-[0-9]{2}$');

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT ck_fin007_paid_amount CHECK (paid_amount >= 0);

ALTER TABLE finances.fin007_card_statement
ADD CONSTRAINT fk_fin007_card_id FOREIGN KEY (card_id)
	REFERENCES finances.fin006_card (id);

CREATE INDEX ix_fin007_user_status_due_date
ON finances.fin007_card_statement (user_id, status, due_date);

CREATE INDEX ix_fin007_card_closing_date
ON finances.fin007_card_statement (card_id, closing_date);

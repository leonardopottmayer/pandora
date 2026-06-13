-- 20260612120006-create-table-fin008-transaction.up.sql

CREATE TABLE finances.fin008_transaction (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	account_id uuid NOT NULL,
	kind VARCHAR(30) NOT NULL,
	status VARCHAR(10) NOT NULL DEFAULT 'posted',
	amount NUMERIC(20,8) NOT NULL,
	currency VARCHAR(10) NOT NULL,
	occurred_on DATE NOT NULL,
	description VARCHAR(255) NOT NULL,
	payee VARCHAR(150) NULL,
	notes TEXT NULL,
	system_category_id uuid NULL,
	user_category_id uuid NULL,
	transfer_group_id uuid NULL,
	fx_rate NUMERIC(20,10) NULL,
	origin VARCHAR(15) NOT NULL DEFAULT 'manual',
	posted_at TIMESTAMPTZ NULL,
	voided_at TIMESTAMPTZ NULL,
	void_reason VARCHAR(255) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT pk_fin008 PRIMARY KEY (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_kind
CHECK (kind IN ('opening-balance', 'income', 'expense', 'transfer-in', 'transfer-out',
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_status
CHECK (status IN ('pending', 'posted', 'void'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_origin
CHECK (origin IN ('manual', 'import', 'recurrence', 'projection'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_amount CHECK (amount > 0);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_account_id FOREIGN KEY (account_id)
	REFERENCES finances.fin001_account (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_system_category_id FOREIGN KEY (system_category_id)
	REFERENCES finances.fin002_system_category (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_user_category_id FOREIGN KEY (user_category_id)
	REFERENCES finances.fin003_user_category (id);

CREATE INDEX ix_fin008_user_occurred_on
ON finances.fin008_transaction (user_id, occurred_on);

CREATE INDEX ix_fin008_account_status_occurred_on
ON finances.fin008_transaction (account_id, status, occurred_on);

CREATE INDEX ix_fin008_transfer_group_id
ON finances.fin008_transaction (transfer_group_id);

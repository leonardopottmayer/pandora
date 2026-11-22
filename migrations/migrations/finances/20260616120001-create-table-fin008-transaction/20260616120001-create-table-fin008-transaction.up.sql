-- 20260616120001-create-table-fin008-transaction.up.sql
-- Created after fin006/fin007/fin009/fin010 so every destination/link FK is inline. The only
-- forward reference is pending_transaction_id -> fin011, which is a circular pair (fin011 also
-- references fin008); that one FK is added separately once fin011 exists, in the next migration.

CREATE TABLE finances.fin008_transaction (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	account_id uuid NULL,
	kind VARCHAR(30) NOT NULL,
	status VARCHAR(10) NOT NULL DEFAULT 'posted',
	amount NUMERIC(20,8) NOT NULL,
	currency VARCHAR(10) NOT NULL,
	occurred_on DATE NOT NULL,
	description VARCHAR(255) NOT NULL,
	system_description JSONB NULL,
	payee VARCHAR(150) NULL,
	notes TEXT NULL,
	system_category_id uuid NULL,
	user_category_id uuid NULL,
	transfer_group_id uuid NULL,
	fx_rate NUMERIC(20,10) NULL,
	origin VARCHAR(15) NOT NULL DEFAULT 'manual',
	card_statement_id uuid NULL,
	card_id uuid NULL,
	paid_statement_id uuid NULL,
	installment_plan_id uuid NULL,
	installment_number SMALLINT NULL,
	reversed_transaction_id uuid NULL,
	pending_transaction_id uuid NULL,
	recurring_transaction_id uuid NULL,
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
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment',
	'refund', 'card-statement-payment', 'statement-writeoff'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_status
CHECK (status IN ('pending', 'posted', 'void'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_origin
CHECK (origin IN ('manual', 'import', 'recurrence', 'projection', 'reversal'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_amount CHECK (amount > 0);

-- Destination is exactly one of account or card statement, except a statement-writeoff which has
-- neither (it only carries paid_statement_id -- the cashless counter-entry of a pre-Pandora debt).
ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_target_xor
CHECK (
	(kind = 'statement-writeoff' AND account_id IS NULL AND card_statement_id IS NULL AND paid_statement_id IS NOT NULL) OR
	(account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(account_id IS NULL AND card_statement_id IS NOT NULL)
);

-- paid_statement_id is set by an account payment or a cashless writeoff.
ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_paid_statement_account_only
CHECK (
	paid_statement_id IS NULL OR
	(kind = 'card-statement-payment' AND account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(kind = 'statement-writeoff' AND account_id IS NULL AND card_statement_id IS NULL)
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_installment_number
CHECK (installment_number IS NULL OR installment_number >= 1);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_installment_pairing
CHECK (
	(installment_plan_id IS NULL AND installment_number IS NULL) OR
	(installment_plan_id IS NOT NULL AND installment_number IS NOT NULL)
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_reversed_transaction_not_self
CHECK (reversed_transaction_id IS NULL OR reversed_transaction_id <> id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_account_id FOREIGN KEY (account_id)
	REFERENCES finances.fin001_account (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_system_category_id FOREIGN KEY (system_category_id)
	REFERENCES finances.fin002_system_category (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_user_category_id FOREIGN KEY (user_category_id)
	REFERENCES finances.fin003_user_category (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_card_statement_id FOREIGN KEY (card_statement_id)
	REFERENCES finances.fin007_card_statement (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_card_id FOREIGN KEY (card_id)
	REFERENCES finances.fin006_card (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_paid_statement_id FOREIGN KEY (paid_statement_id)
	REFERENCES finances.fin007_card_statement (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_installment_plan_id FOREIGN KEY (installment_plan_id)
	REFERENCES finances.fin009_installment_plan (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_reversed_transaction_id FOREIGN KEY (reversed_transaction_id)
	REFERENCES finances.fin008_transaction (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT uq_fin008_reversed_transaction_id UNIQUE (reversed_transaction_id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_recurring_transaction_id FOREIGN KEY (recurring_transaction_id)
	REFERENCES finances.fin010_recurring_transaction (id);

CREATE INDEX ix_fin008_user_occurred_on
ON finances.fin008_transaction (user_id, occurred_on);

CREATE INDEX ix_fin008_account_status_occurred_on
ON finances.fin008_transaction (account_id, status, occurred_on);

CREATE INDEX ix_fin008_transfer_group_id
ON finances.fin008_transaction (transfer_group_id);

CREATE INDEX ix_fin008_card_statement_status_occurred_on
ON finances.fin008_transaction (card_statement_id, status, occurred_on);

CREATE INDEX ix_fin008_paid_statement_id
ON finances.fin008_transaction (paid_statement_id);

CREATE INDEX ix_fin008_card_id_occurred_on
ON finances.fin008_transaction (card_id, occurred_on);

CREATE INDEX ix_fin008_installment_plan_id
ON finances.fin008_transaction (installment_plan_id);

CREATE INDEX ix_fin008_reversed_transaction_id
ON finances.fin008_transaction (reversed_transaction_id);

CREATE INDEX ix_fin008_recurring_transaction_id
ON finances.fin008_transaction (recurring_transaction_id);

CREATE INDEX ix_fin008_pending_transaction_id
ON finances.fin008_transaction (pending_transaction_id);

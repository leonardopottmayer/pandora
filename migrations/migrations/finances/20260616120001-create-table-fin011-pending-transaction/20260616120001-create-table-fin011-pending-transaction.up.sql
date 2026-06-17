-- 20260616120001-create-table-fin011-pending-transaction.up.sql
-- Phase 08: source = 'recurrence' only; CHECK expanded in phase 09 to include 'import'.

CREATE TABLE finances.fin011_pending_transaction (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	source varchar(15) NOT NULL DEFAULT 'recurrence',
	recurring_transaction_id uuid NULL,
	-- proposed payload (editable until decided)
	account_id uuid NULL,
	card_id uuid NULL,
	kind varchar(30) NOT NULL,
	amount numeric(20,8) NULL,
	currency varchar(10) NOT NULL,
	occurred_on date NOT NULL,
	description varchar(255) NOT NULL,
	payee varchar(150) NULL,
	notes text NULL,
	system_category_id uuid NULL,
	user_category_id uuid NULL,
	suggested_statement_id uuid NULL,
	-- immutable payload — initial snapshot, never changed
	original_payload jsonb NOT NULL,
	-- decision
	status varchar(10) NOT NULL DEFAULT 'pending',
	decided_at TIMESTAMPTZ NULL,
	decided_by uuid NULL,
	rejection_reason varchar(255) NULL,
	transaction_id uuid NULL,
	-- audit
	created_by uuid NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by uuid NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT pk_fin011 PRIMARY KEY (id);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_source CHECK (source IN ('recurrence'));

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_status CHECK (status IN ('pending', 'approved', 'rejected'));

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_recurrence_source
CHECK (source <> 'recurrence' OR recurring_transaction_id IS NOT NULL);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT fk_fin011_recurring_transaction_id FOREIGN KEY (recurring_transaction_id)
	REFERENCES finances.fin010_recurring_transaction (id);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT fk_fin011_suggested_statement_id FOREIGN KEY (suggested_statement_id)
	REFERENCES finances.fin007_card_statement (id);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT fk_fin011_transaction_id FOREIGN KEY (transaction_id)
	REFERENCES finances.fin008_transaction (id);

-- Idempotency: only one pending entry can exist per recurrence + date
CREATE UNIQUE INDEX uq_fin011_recurrence_occurrence
ON finances.fin011_pending_transaction (recurring_transaction_id, occurred_on)
WHERE recurring_transaction_id IS NOT NULL;

CREATE INDEX ix_fin011_user_status
ON finances.fin011_pending_transaction (user_id, status);

CREATE INDEX ix_fin011_recurring_transaction_id
ON finances.fin011_pending_transaction (recurring_transaction_id);

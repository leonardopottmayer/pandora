-- 20260612120009-alter-table-fin008-add-card-columns.up.sql

ALTER TABLE finances.fin008_transaction
ALTER COLUMN account_id DROP NOT NULL;

ALTER TABLE finances.fin008_transaction
ADD COLUMN card_statement_id uuid NULL,
ADD COLUMN card_id uuid NULL,
ADD COLUMN paid_statement_id uuid NULL;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_kind;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_kind
CHECK (kind IN ('opening-balance', 'income', 'expense', 'transfer-in', 'transfer-out',
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment',
	'refund', 'card-statement-payment'));

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_target_xor
CHECK (
	(account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(account_id IS NULL AND card_statement_id IS NOT NULL)
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_paid_statement_account_only
CHECK (
	paid_statement_id IS NULL OR
	(kind = 'card-statement-payment' AND account_id IS NOT NULL AND card_statement_id IS NULL)
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_card_statement_id FOREIGN KEY (card_statement_id)
	REFERENCES finances.fin007_card_statement (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_card_id FOREIGN KEY (card_id)
	REFERENCES finances.fin006_card (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_paid_statement_id FOREIGN KEY (paid_statement_id)
	REFERENCES finances.fin007_card_statement (id);

CREATE INDEX ix_fin008_card_statement_status_occurred_on
ON finances.fin008_transaction (card_statement_id, status, occurred_on);

CREATE INDEX ix_fin008_paid_statement_id
ON finances.fin008_transaction (paid_statement_id);

CREATE INDEX ix_fin008_card_id_occurred_on
ON finances.fin008_transaction (card_id, occurred_on);

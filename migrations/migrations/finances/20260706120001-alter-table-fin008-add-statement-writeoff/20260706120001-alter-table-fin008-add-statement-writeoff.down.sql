-- 20260706120001-alter-table-fin008-add-statement-writeoff.down.sql
-- Reverts to the account-only paid-statement rules. Assumes no 'statement-writeoff' rows remain.

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_paid_statement_account_only;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_paid_statement_account_only
CHECK (
	paid_statement_id IS NULL OR
	(kind = 'card-statement-payment' AND account_id IS NOT NULL AND card_statement_id IS NULL)
);

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_target_xor;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_target_xor
CHECK (
	(account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(account_id IS NULL AND card_statement_id IS NOT NULL)
);

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_kind;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_kind
CHECK (kind IN ('opening-balance', 'income', 'expense', 'transfer-in', 'transfer-out',
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment',
	'refund', 'card-statement-payment'));

-- 20260706120001-alter-table-fin008-add-statement-writeoff.up.sql
-- Cashless statement settlement (onboarding): a 'statement-writeoff' clears a statement's balance
-- without debiting any account. It is a durable ledger row (so it survives paid_amount recomputes)
-- that carries paid_statement_id but has NO account_id and NO card_statement_id — the counter-entry
-- of a pre-Pandora debt, mirroring how opening-balance has no counterparty.

-- Allow the new kind.
ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_kind;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_kind
CHECK (kind IN ('opening-balance', 'income', 'expense', 'transfer-in', 'transfer-out',
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment',
	'refund', 'card-statement-payment', 'statement-writeoff'));

-- Relax the destination XOR: a writeoff has neither account_id nor card_statement_id.
ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_target_xor;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_target_xor
CHECK (
	(kind = 'statement-writeoff' AND account_id IS NULL AND card_statement_id IS NULL AND paid_statement_id IS NOT NULL) OR
	(account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(account_id IS NULL AND card_statement_id IS NOT NULL)
);

-- paid_statement_id is set by two kinds now: an account payment or a cashless writeoff.
ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_paid_statement_account_only;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_paid_statement_account_only
CHECK (
	paid_statement_id IS NULL OR
	(kind = 'card-statement-payment' AND account_id IS NOT NULL AND card_statement_id IS NULL) OR
	(kind = 'statement-writeoff' AND account_id IS NULL AND card_statement_id IS NULL)
);

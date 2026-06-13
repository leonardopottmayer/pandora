-- 20260612120009-alter-table-fin008-add-card-columns.down.sql

DROP INDEX finances.ix_fin008_card_id_occurred_on;
DROP INDEX finances.ix_fin008_paid_statement_id;
DROP INDEX finances.ix_fin008_card_statement_status_occurred_on;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_paid_statement_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_card_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_card_statement_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_paid_statement_account_only;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_target_xor;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_kind;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_kind
CHECK (kind IN ('opening-balance', 'income', 'expense', 'transfer-in', 'transfer-out',
	'investment-contribution', 'investment-redemption', 'yield', 'adjustment'));

ALTER TABLE finances.fin008_transaction
DROP COLUMN paid_statement_id,
DROP COLUMN card_id,
DROP COLUMN card_statement_id;

ALTER TABLE finances.fin008_transaction
ALTER COLUMN account_id SET NOT NULL;

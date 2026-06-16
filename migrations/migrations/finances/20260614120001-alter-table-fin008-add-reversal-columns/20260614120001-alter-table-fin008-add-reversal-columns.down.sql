-- 20260614120001-alter-table-fin008-add-reversal-columns.down.sql

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_origin;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_origin
CHECK (origin IN ('manual', 'import', 'recurrence', 'projection'));

DROP INDEX finances.ix_fin008_reversed_transaction_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_reversed_transaction_not_self;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT uq_fin008_reversed_transaction_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_reversed_transaction_id;

ALTER TABLE finances.fin008_transaction
DROP COLUMN reversed_transaction_id;

-- 20260613120001-alter-table-fin008-add-installment-columns.down.sql

DROP INDEX finances.ix_fin008_installment_plan_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_installment_plan_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_installment_pairing;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_installment_number;

ALTER TABLE finances.fin008_transaction
DROP COLUMN installment_number,
DROP COLUMN installment_plan_id;

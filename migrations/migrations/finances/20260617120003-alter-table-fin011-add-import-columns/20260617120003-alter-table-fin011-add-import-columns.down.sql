-- 20260617120003-alter-table-fin011-add-import-columns.down.sql
DROP INDEX IF EXISTS finances.ix_fin011_import_row_id;

ALTER TABLE finances.fin011_pending_transaction DROP CONSTRAINT IF EXISTS ck_fin011_dedup_status;
ALTER TABLE finances.fin011_pending_transaction DROP CONSTRAINT IF EXISTS ck_fin011_import_source;

ALTER TABLE finances.fin011_pending_transaction DROP CONSTRAINT IF EXISTS ck_fin011_source;
ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_source CHECK (source IN ('recurrence'));

ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS matched_installment_plan_id;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS installment_count;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS installment_number;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS dedup_status;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS duplicate_of_pending_id;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS duplicate_of_transaction_id;
ALTER TABLE finances.fin011_pending_transaction DROP COLUMN IF EXISTS import_row_id;

-- 20260616120002-alter-table-fin008-add-recurrence-refs.down.sql

ALTER TABLE finances.fin008_transaction DROP CONSTRAINT fk_fin008_pending_transaction_id;
ALTER TABLE finances.fin008_transaction DROP CONSTRAINT fk_fin008_recurring_transaction_id;
DROP INDEX finances.ix_fin008_pending_transaction_id;
DROP INDEX finances.ix_fin008_recurring_transaction_id;
ALTER TABLE finances.fin008_transaction DROP COLUMN pending_transaction_id;
ALTER TABLE finances.fin008_transaction DROP COLUMN recurring_transaction_id;

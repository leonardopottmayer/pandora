-- 20260620130000-alter-table-fin010-add-auto-generate.down.sql

ALTER TABLE finances.fin010_recurring_transaction
DROP COLUMN auto_generate;

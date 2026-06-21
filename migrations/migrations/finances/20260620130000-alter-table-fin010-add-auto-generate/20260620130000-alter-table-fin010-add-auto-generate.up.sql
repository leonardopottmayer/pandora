-- 20260620130000-alter-table-fin010-add-auto-generate.up.sql

ALTER TABLE finances.fin010_recurring_transaction
ADD COLUMN auto_generate boolean NOT NULL DEFAULT true;

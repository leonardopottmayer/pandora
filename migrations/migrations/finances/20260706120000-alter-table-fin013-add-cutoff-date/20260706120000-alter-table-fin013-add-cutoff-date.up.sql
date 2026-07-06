-- 20260706120000-alter-table-fin013-add-cutoff-date.up.sql
-- Onboarding cutoff: an optional date set at upload time. During parsing, rows dated before this
-- date are skipped (no suggestion generated), so importing historical bank files at go-live does not
-- flood the inbox with pre-onboarding movements. NULL means no cutoff (import everything).

ALTER TABLE finances.fin013_import_file
ADD COLUMN cutoff_date date NULL;

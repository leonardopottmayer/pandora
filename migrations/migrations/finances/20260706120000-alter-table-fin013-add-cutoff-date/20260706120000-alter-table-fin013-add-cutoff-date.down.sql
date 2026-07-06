-- 20260706120000-alter-table-fin013-add-cutoff-date.down.sql

ALTER TABLE finances.fin013_import_file
DROP COLUMN cutoff_date;

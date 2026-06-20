-- 20260617120003-alter-table-fin011-add-import-columns.up.sql
-- Phase 09: extend fin011 so import-sourced suggestions carry their provenance and dedup links.
-- A suggestion is always generated even when a duplicate is detected; the dedup fields allow the
-- UI to surface the relationship and let the user decide what to do.

-- Link to the import row that originated this suggestion
ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN import_row_id uuid NULL;

-- Dedup: link to an existing transaction that this row likely duplicates
ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN duplicate_of_transaction_id uuid NULL;

-- Dedup: link to an existing pending suggestion that this row likely duplicates
ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN duplicate_of_pending_id uuid NULL;

-- Certainty level of the dedup match (NULL when source <> 'import')
ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN dedup_status varchar(15) NULL;

-- Installment info extracted from description (phase 10)
ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN installment_number smallint NULL;

ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN installment_count smallint NULL;

ALTER TABLE finances.fin011_pending_transaction
ADD COLUMN matched_installment_plan_id uuid NULL;

-- Expand the source CHECK to include 'import'
ALTER TABLE finances.fin011_pending_transaction
DROP CONSTRAINT ck_fin011_source;

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_source CHECK (source IN ('recurrence', 'import'));

-- New rule: import-sourced suggestions must have an import_row_id
ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_import_source
CHECK (source <> 'import' OR import_row_id IS NOT NULL);

ALTER TABLE finances.fin011_pending_transaction
ADD CONSTRAINT ck_fin011_dedup_status
CHECK (dedup_status IS NULL OR dedup_status IN ('new', 'certain', 'suspected', 'matched'));

CREATE INDEX ix_fin011_import_row_id
ON finances.fin011_pending_transaction (import_row_id)
WHERE import_row_id IS NOT NULL;

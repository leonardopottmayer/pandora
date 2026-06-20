-- 20260617120002-create-table-fin014-import-row.up.sql
-- Phase 09+10: one row per data line extracted from the imported file.
-- raw_data preserves the original bytes; parsed_payload holds the structured interpretation.
-- dedup_key is a SHA-256 hex computed from the row's identity fields; matched_* link to existing
-- entities when a duplicate or pending reconciliation is found.
-- installment_* fields are populated in phase 10 when the parser detects a parcela pattern.

CREATE TABLE finances.fin014_import_row (
    id                              uuid          NOT NULL DEFAULT uuid_generate_v7(),
    import_file_id                  uuid          NOT NULL,
    row_index                       int           NOT NULL,
    raw_data                        text          NOT NULL,
    parsed_payload                  jsonb         NULL,
    -- dedup
    external_id                     varchar(255)  NULL,   -- FITID or CSV identifier column
    dedup_key                       varchar(64)   NULL,   -- sha256 hex of identity fields
    dedup_status                    varchar(15)   NOT NULL DEFAULT 'new',
    matched_transaction_id          uuid          NULL,   -- existing transaction (certain/suspected match)
    matched_pending_transaction_id  uuid          NULL,   -- existing pending suggestion (recurrence match)
    -- installment detection (phase 10)
    installment_number              smallint      NULL,
    installment_count               smallint      NULL,
    matched_installment_plan_id     uuid          NULL,
    -- outcome
    pending_transaction_id          uuid          NULL,   -- PendingTransaction created for this row
    status                          varchar(20)   NOT NULL DEFAULT 'pending',
    error_message                   text          NULL,
    created_at                      TIMESTAMPTZ   NOT NULL DEFAULT current_timestamp
);

ALTER TABLE finances.fin014_import_row
ADD CONSTRAINT pk_fin014 PRIMARY KEY (id);

ALTER TABLE finances.fin014_import_row
ADD CONSTRAINT ck_fin014_dedup_status
CHECK (dedup_status IN ('new', 'certain', 'suspected', 'matched'));

ALTER TABLE finances.fin014_import_row
ADD CONSTRAINT ck_fin014_status
CHECK (status IN ('pending', 'suggestion-created', 'skipped', 'error'));

ALTER TABLE finances.fin014_import_row
ADD CONSTRAINT fk_fin014_import_file_id FOREIGN KEY (import_file_id)
    REFERENCES finances.fin013_import_file (id);

-- FK to fin011 and fin008 are logical only (no physical FK) to avoid cross-import coupling
-- and allow rows to be queried independently. The application enforces referential integrity.

CREATE INDEX ix_fin014_import_file_id
ON finances.fin014_import_row (import_file_id);

CREATE INDEX ix_fin014_dedup_key
ON finances.fin014_import_row (dedup_key)
WHERE dedup_key IS NOT NULL;

CREATE INDEX ix_fin014_external_id
ON finances.fin014_import_row (external_id)
WHERE external_id IS NOT NULL;

CREATE INDEX ix_fin014_pending_transaction_id
ON finances.fin014_import_row (pending_transaction_id)
WHERE pending_transaction_id IS NOT NULL;

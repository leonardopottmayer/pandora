-- 20260617120001-create-table-fin013-import-file.up.sql
-- Phase 09: one row per uploaded file. The file bytes are stored as bytea so the parsing job can
-- re-read them on retry without needing the original upload. The sha256 hex is stored for
-- informational use (UI can warn the user about duplicate uploads) but is NOT unique — users may
-- intentionally re-import the same file to rebuild suggestions.

CREATE TABLE finances.fin013_import_file (
    id              uuid          NOT NULL DEFAULT uuid_generate_v7(),
    user_id         uuid          NOT NULL,
    layout_id       uuid          NULL,      -- NULL when detection failed
    account_id      uuid          NULL,      -- XOR with card_id
    card_id         uuid          NULL,
    file_name       varchar(255)  NOT NULL,
    file_hash       varchar(64)   NOT NULL,  -- sha256 hex, informational only
    file_content    bytea         NOT NULL,
    file_size       int           NOT NULL,
    correlation_id  uuid          NOT NULL,
    status          varchar(15)   NOT NULL DEFAULT 'received',
    -- counters (updated incrementally during parsing)
    total_rows      int           NOT NULL DEFAULT 0,
    parsed_rows     int           NOT NULL DEFAULT 0,
    error_rows      int           NOT NULL DEFAULT 0,
    duplicate_rows  int           NOT NULL DEFAULT 0,
    suggestion_rows int           NOT NULL DEFAULT 0,
    -- fault tolerance
    retry_count     int           NOT NULL DEFAULT 0,
    error_message   text          NULL,
    -- timestamps
    started_at      TIMESTAMPTZ   NULL,
    completed_at    TIMESTAMPTZ   NULL,
    created_by      uuid          NULL,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT current_timestamp,
    updated_by      uuid          NULL,
    updated_at      TIMESTAMPTZ   NULL
);

ALTER TABLE finances.fin013_import_file
ADD CONSTRAINT pk_fin013 PRIMARY KEY (id);

ALTER TABLE finances.fin013_import_file
ADD CONSTRAINT ck_fin013_status
CHECK (status IN ('received', 'parsing', 'completed', 'failed', 'aborted'));

ALTER TABLE finances.fin013_import_file
ADD CONSTRAINT ck_fin013_destination
CHECK (
    (account_id IS NOT NULL AND card_id IS NULL)
    OR (account_id IS NULL AND card_id IS NOT NULL)
);

ALTER TABLE finances.fin013_import_file
ADD CONSTRAINT fk_fin013_layout_id FOREIGN KEY (layout_id)
    REFERENCES finances.fin012_import_layout (id);

CREATE INDEX ix_fin013_user_status
ON finances.fin013_import_file (user_id, status);

CREATE INDEX ix_fin013_status_created
ON finances.fin013_import_file (status, created_at)
WHERE status = 'received';

CREATE INDEX ix_fin013_correlation_id
ON finances.fin013_import_file (correlation_id);

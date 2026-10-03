-- 20261003120000-create-table-fin018-file-blob.up.sql

-- The bytes behind the module's IFileStorage (DatabaseFileStorage, keyed "finances"). The row id is
-- the storage key an attachment (fin017) points at. Same shape as notes.nte003_file_blob: each module
-- keeps its own blobs, so a future S3 backend needs no migration here.
CREATE TABLE finances.fin018_file_blob (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	file_name VARCHAR(255) NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	content BYTEA NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE finances.fin018_file_blob
ADD CONSTRAINT pk_fin018 PRIMARY KEY (id);

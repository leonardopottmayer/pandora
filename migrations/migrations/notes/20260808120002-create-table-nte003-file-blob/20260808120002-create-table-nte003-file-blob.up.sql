-- 20260808120002-create-table-nte003-file-blob.up.sql

-- Generic blob store behind IFileStorage/DatabaseFileStorage. The row id is the storage key an
-- attachment points at. Self-descriptive columns mean a future S3 backend needs no migration here.
CREATE TABLE notes.nte003_file_blob (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	file_name VARCHAR(255) NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	content BYTEA NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE notes.nte003_file_blob
ADD CONSTRAINT pk_nte003 PRIMARY KEY (id);

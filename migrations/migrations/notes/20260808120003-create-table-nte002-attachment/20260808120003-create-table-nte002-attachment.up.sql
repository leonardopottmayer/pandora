-- 20260808120003-create-table-nte002-attachment.up.sql

-- An uploaded file's metadata. The bytes live in an IFileStorage backend located by
-- (storage_backend, storage_key); in the MVP that is always the notes.nte003_file_blob table.
CREATE TABLE notes.nte002_attachment (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NULL,
	file_name VARCHAR(255) NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	storage_backend VARCHAR(50) NOT NULL,
	storage_key VARCHAR(1024) NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE notes.nte002_attachment
ADD CONSTRAINT pk_nte002 PRIMARY KEY (id);

-- Loose reference (no FK): a page may be soft-deleted while its attachment lingers.
CREATE INDEX ix_nte002_page_id
ON notes.nte002_attachment (page_id);

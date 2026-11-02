-- 20260808120005-create-table-nte002-media.up.sql

-- An item of the media library: an image or a file today, a video one day (kind). The bytes live in
-- an IFileStorage backend located by (storage_backend, storage_key) -- the notes.nte003_file_blob
-- table for now. Derived versions of it, like the thumbnail, are nte009 rows.
CREATE TABLE notes.nte002_media (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	-- NULL is "Unsorted".
	folder_id uuid NULL,
	kind VARCHAR(20) NOT NULL,
	-- What the library shows; the original name stays as the download's file name.
	name VARCHAR(255) NOT NULL,
	original_file_name VARCHAR(255) NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	-- Pixels, for images (and videos); NULL for a plain file or an image that could not be read.
	width INT NULL,
	height INT NULL,
	storage_backend VARCHAR(50) NOT NULL,
	storage_key VARCHAR(1024) NOT NULL,
	deleted_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	created_by uuid NULL,
	updated_at TIMESTAMPTZ NULL,
	updated_by uuid NULL,
	CONSTRAINT chk_nte002_kind CHECK (kind IN ('image', 'file', 'video'))
);

ALTER TABLE notes.nte002_media
ADD CONSTRAINT pk_nte002 PRIMARY KEY (id);

ALTER TABLE notes.nte002_media
ADD CONSTRAINT fk_nte002_folder
FOREIGN KEY (folder_id) REFERENCES notes.nte008_media_folder (id);

-- Media are scoped to the uploader: another user's id answers 404.
CREATE INDEX ix_nte002_user_id
ON notes.nte002_media (user_id);

CREATE INDEX ix_nte002_folder_id
ON notes.nte002_media (folder_id);

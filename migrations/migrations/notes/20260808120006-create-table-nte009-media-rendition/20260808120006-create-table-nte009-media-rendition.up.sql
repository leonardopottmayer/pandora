-- 20260808120006-create-table-nte009-media-rendition.up.sql

-- A version derived from a media item, with bytes of its own: the thumbnail of an image today, the
-- poster frame or a smaller encoding of a video one day. Written once when the item is uploaded.
CREATE TABLE notes.nte009_media_rendition (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	media_id uuid NOT NULL,
	purpose VARCHAR(20) NOT NULL,
	width INT NOT NULL,
	height INT NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	storage_backend VARCHAR(50) NOT NULL,
	storage_key VARCHAR(1024) NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	CONSTRAINT chk_nte009_purpose CHECK (purpose IN ('thumbnail'))
);

ALTER TABLE notes.nte009_media_rendition
ADD CONSTRAINT pk_nte009 PRIMARY KEY (id);

ALTER TABLE notes.nte009_media_rendition
ADD CONSTRAINT fk_nte009_media
FOREIGN KEY (media_id) REFERENCES notes.nte002_media (id);

-- One of each purpose per item.
CREATE UNIQUE INDEX uq_nte009_media_purpose
ON notes.nte009_media_rendition (media_id, purpose);

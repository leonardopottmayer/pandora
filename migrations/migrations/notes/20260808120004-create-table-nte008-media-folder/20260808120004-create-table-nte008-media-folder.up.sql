-- 20260808120004-create-table-nte008-media-folder.up.sql

-- A virtual folder of the media library: a tree of names to organize uploads by, nothing on disk.
-- Media with no folder sit in "Unsorted". The library is the user's, shared by every space.
CREATE TABLE notes.nte008_media_folder (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	parent_id uuid NULL,
	name VARCHAR(100) NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	created_by uuid NULL,
	updated_at TIMESTAMPTZ NULL,
	updated_by uuid NULL
);

ALTER TABLE notes.nte008_media_folder
ADD CONSTRAINT pk_nte008 PRIMARY KEY (id);

-- Deleting a folder moves what it holds up a level first, so a child never points at a missing row.
ALTER TABLE notes.nte008_media_folder
ADD CONSTRAINT fk_nte008_parent
FOREIGN KEY (parent_id) REFERENCES notes.nte008_media_folder (id);

CREATE INDEX ix_nte008_user_id
ON notes.nte008_media_folder (user_id);

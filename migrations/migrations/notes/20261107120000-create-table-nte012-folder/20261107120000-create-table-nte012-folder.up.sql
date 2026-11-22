-- 20261107120000-create-table-nte012-folder.up.sql

-- A folder of a space: it holds folders and pages (nte001.folder_id), never inside a page. A folder
-- and its parent share a space. Deleting is soft and takes what the folder holds with it.
CREATE TABLE notes.nte012_folder (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	space_id uuid NOT NULL,
	parent_id uuid NULL,
	name VARCHAR(255) NOT NULL,
	icon VARCHAR(50) NULL,
	description TEXT NULL,
	order_index INT NOT NULL DEFAULT 0,
	deleted_at TIMESTAMPTZ NULL,
	created_by uuid NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by uuid NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE notes.nte012_folder
ADD CONSTRAINT pk_nte012 PRIMARY KEY (id);

ALTER TABLE notes.nte012_folder
ADD CONSTRAINT fk_nte012_space
FOREIGN KEY (space_id) REFERENCES notes.nte007_space (id);

ALTER TABLE notes.nte012_folder
ADD CONSTRAINT fk_nte012_parent
FOREIGN KEY (parent_id) REFERENCES notes.nte012_folder (id);

CREATE INDEX ix_nte012_user_id
ON notes.nte012_folder (user_id);

CREATE INDEX ix_nte012_space_id
ON notes.nte012_folder (space_id);

-- The page's folder column is in nte001's CREATE; its key waits for this table.
ALTER TABLE notes.nte001_page
ADD CONSTRAINT fk_nte001_folder
FOREIGN KEY (folder_id) REFERENCES notes.nte012_folder (id);

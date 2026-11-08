-- 20261102140000-create-table-nte011-page-version.up.sql

-- Full immutable snapshots, retained indefinitely. No foreign keys from snapshot metadata:
-- moving/deleting a parent or space must not rewrite a historical state.
CREATE TABLE notes.nte011_page_version (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NOT NULL,
	user_id uuid NOT NULL,
	number bigint NOT NULL,
	created_at timestamptz NOT NULL,
	source varchar(20) NOT NULL,
	restored_from bigint NULL,
	snapshot jsonb NOT NULL
);

ALTER TABLE notes.nte011_page_version
ADD CONSTRAINT pk_nte011 PRIMARY KEY (id);

ALTER TABLE notes.nte011_page_version
ADD CONSTRAINT fk_nte011_page
FOREIGN KEY (page_id) REFERENCES notes.nte001_page (id);

CREATE UNIQUE INDEX uq_nte011_page_number
ON notes.nte011_page_version (page_id, number);

-- Existing pages start here; this does not claim to reconstruct their earlier history.
INSERT INTO notes.nte011_page_version (page_id, user_id, number, created_at, source, snapshot)
SELECT id, user_id, 1, current_timestamp, 'initial', jsonb_build_object(
	'Title', title, 'Content', content::text, 'Icon', icon,
	'SpaceId', space_id, 'ParentId', parent_id, 'FolderId', folder_id, 'OrderIndex', order_index,
	'IsFavorite', is_favorite, 'ArchivedAt', archived_at, 'DeletedAt', deleted_at
)
FROM notes.nte001_page;

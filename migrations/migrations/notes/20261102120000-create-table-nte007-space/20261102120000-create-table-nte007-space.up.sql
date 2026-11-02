-- 20261102120000-create-table-nte007-space.up.sql

-- A space groups pages the way a Confluence space does: every page belongs to exactly one, and the
-- sidebar shows one space's tree at a time. Tags and wikilinks are not scoped by it -- a tag or a
-- link reaches pages in any space.
CREATE TABLE notes.nte007_space (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	icon VARCHAR(50) NULL,
	color VARCHAR(20) NULL,
	description VARCHAR(500) NULL,
	-- Where a page goes when nothing says which space (the assistant, an API call without one).
	is_default BOOLEAN NOT NULL DEFAULT false,
	archived_at TIMESTAMPTZ NULL,
	deleted_at TIMESTAMPTZ NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	created_by uuid NULL,
	updated_at TIMESTAMPTZ NULL,
	updated_by uuid NULL
);

ALTER TABLE notes.nte007_space
ADD CONSTRAINT pk_nte007 PRIMARY KEY (id);

CREATE INDEX ix_nte007_user_id
ON notes.nte007_space (user_id);

-- At most one default space per user among the live ones.
CREATE UNIQUE INDEX uq_nte007_user_default
ON notes.nte007_space (user_id)
WHERE is_default AND deleted_at IS NULL;

-- Declared here rather than in nte001's migration, which runs before this table exists. A space is
-- only ever soft-deleted, and only when empty, so its pages never point at a missing row.
ALTER TABLE notes.nte001_page
ADD CONSTRAINT fk_nte001_space
FOREIGN KEY (space_id) REFERENCES notes.nte007_space (id);

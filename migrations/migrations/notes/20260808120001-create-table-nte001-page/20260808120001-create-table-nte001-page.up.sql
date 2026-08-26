-- 20260808120001-create-table-nte001-page.up.sql

CREATE TABLE notes.nte001_page (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	parent_id uuid NULL,
	title VARCHAR(255) NOT NULL,
	slug VARCHAR(100) NOT NULL,
	content_markdown TEXT NOT NULL DEFAULT '',
	-- Full-text search over the page: title and body in a single vector, kept in sync by Postgres
	-- itself (generated column) so no save path can forget to update it. The 'simple' configuration
	-- only lower-cases -- no stemming and no language guess, which is what a notebook holding both
	-- PT-BR and EN text needs.
	search_vector tsvector GENERATED ALWAYS AS (
		to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_markdown, ''))
	) STORED,
	icon VARCHAR(50) NULL,
	order_index INT NOT NULL DEFAULT 0,
	is_favorite BOOLEAN NOT NULL DEFAULT false,
	archived_at TIMESTAMPTZ NULL,
	deleted_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE notes.nte001_page
ADD CONSTRAINT pk_nte001 PRIMARY KEY (id);

-- Self-reference for the sidebar hierarchy; a deleted parent must not orphan children, so the
-- delete command soft-deletes the whole subtree rather than relying on a DB cascade.
ALTER TABLE notes.nte001_page
ADD CONSTRAINT fk_nte001_parent
FOREIGN KEY (parent_id) REFERENCES notes.nte001_page (id);

-- Slug is unique per user among live pages only; a soft-deleted page frees its slug for reuse.
CREATE UNIQUE INDEX uq_nte001_user_slug
ON notes.nte001_page (user_id, slug)
WHERE deleted_at IS NULL;

CREATE INDEX ix_nte001_user_id
ON notes.nte001_page (user_id);

CREATE INDEX ix_nte001_parent_id
ON notes.nte001_page (parent_id);

CREATE INDEX ix_nte001_search_vector
ON notes.nte001_page USING GIN (search_vector);

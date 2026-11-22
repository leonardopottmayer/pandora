-- 20260808120002-create-table-nte001-page.up.sql

CREATE TABLE notes.nte001_page (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	-- The space the page lives in (nte007); a child always shares its parent's.
	space_id uuid NOT NULL,
	parent_id uuid NULL,
	-- The folder a top-level page sits in (nte012); a sub-page has none, it lives under its parent.
	folder_id uuid NULL,
	title VARCHAR(255) NOT NULL,
	slug VARCHAR(100) NOT NULL,
	-- The page's blocks (see the Notes product plan): { "version": 1, "blocks": [...] }.
	content jsonb NOT NULL DEFAULT '{"version": 1, "blocks": []}',
	-- The blocks' readable text, written by the backend on every save, for search and excerpts.
	content_text TEXT NOT NULL DEFAULT '',
	-- Full-text search over the page: title and body in a single vector, kept in sync by Postgres
	-- itself (generated column) so no save path can forget to update it. The 'simple' configuration
	-- only lower-cases -- no stemming and no language guess, which is what a notebook holding both
	-- PT-BR and EN text needs.
	search_vector tsvector GENERATED ALWAYS AS (
		to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_text, ''))
	) STORED,
	icon VARCHAR(50) NULL,
	order_index INT NOT NULL DEFAULT 0,
	is_favorite BOOLEAN NOT NULL DEFAULT false,
	is_template BOOLEAN NOT NULL DEFAULT false,
	archived_at TIMESTAMPTZ NULL,
	deleted_at TIMESTAMPTZ NULL,
	-- How the page looks: cover, width, font, small text (see PageAppearance). Not versioned.
	appearance jsonb NOT NULL DEFAULT '{"width": "normal", "font": "default", "smallText": false}',
	-- Locked, the page opens to read until it is unlocked.
	is_locked BOOLEAN NOT NULL DEFAULT false,
	-- When the page was last checked as right, and when that runs out (NULL: it does not).
	verified_at TIMESTAMPTZ NULL,
	verified_until TIMESTAMPTZ NULL,
	-- When the page was last opened, for the recent pages.
	viewed_at TIMESTAMPTZ NULL,
	-- The secret of the page's public link (read-only, no sign-in); NULL while it is private.
	public_token VARCHAR(64) NULL,
	history_number bigint NOT NULL DEFAULT 1,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE notes.nte001_page
ADD CONSTRAINT pk_nte001 PRIMARY KEY (id);

-- Self-reference for the sidebar hierarchy; a deleted parent must not orphan children, so the
-- delete command soft-deletes the whole subtree rather than relying on a DB cascade.
-- A space is only ever soft-deleted, and only when empty, so a page never points at a missing row.
ALTER TABLE notes.nte001_page
ADD CONSTRAINT fk_nte001_space
FOREIGN KEY (space_id) REFERENCES notes.nte007_space (id);

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

CREATE INDEX ix_nte001_folder_id
ON notes.nte001_page (folder_id);

CREATE INDEX ix_nte001_space_id
ON notes.nte001_page (space_id);

-- A public link resolves to exactly one page.
CREATE UNIQUE INDEX uq_nte001_public_token
ON notes.nte001_page (public_token)
WHERE public_token IS NOT NULL;

CREATE INDEX ix_nte001_search_vector
ON notes.nte001_page USING GIN (search_vector);

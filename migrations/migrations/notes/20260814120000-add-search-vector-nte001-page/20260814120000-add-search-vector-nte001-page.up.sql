-- 20260814120000-add-search-vector-nte001-page.up.sql

-- Full-text search over the page: title and body in a single vector, kept in sync by Postgres
-- itself (generated column) so no save path can forget to update it. The 'simple' configuration
-- only lower-cases -- no stemming and no language guess, which is what a notebook holding both
-- PT-BR and EN text needs.
ALTER TABLE notes.nte001_page
ADD COLUMN search_vector tsvector
GENERATED ALWAYS AS (
	to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_markdown, ''))
) STORED;

CREATE INDEX ix_nte001_search_vector
ON notes.nte001_page USING GIN (search_vector);

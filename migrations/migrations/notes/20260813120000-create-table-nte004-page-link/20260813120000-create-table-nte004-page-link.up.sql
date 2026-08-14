-- 20260813120000-create-table-nte004-page-link.up.sql

-- An edge of the wiki graph, derived from the source page's markdown ([[wikilinks]]) and rewritten
-- on every save of that page. Unlike the parent_id tree, this graph may contain cycles.
CREATE TABLE notes.nte004_page_link (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	source_page_id uuid NOT NULL,
	target_page_id uuid NOT NULL,
	kind VARCHAR(20) NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE notes.nte004_page_link
ADD CONSTRAINT pk_nte004 PRIMARY KEY (id);

-- Both endpoints are real pages; the rows survive a soft-delete of either side, so a target that is
-- soft-deleted leaves a "broken" edge that reads filter out.
ALTER TABLE notes.nte004_page_link
ADD CONSTRAINT fk_nte004_source
FOREIGN KEY (source_page_id) REFERENCES notes.nte001_page (id);

ALTER TABLE notes.nte004_page_link
ADD CONSTRAINT fk_nte004_target
FOREIGN KEY (target_page_id) REFERENCES notes.nte001_page (id);

-- One edge per (source, target, kind): a target linked twice in the same page is a single fact.
CREATE UNIQUE INDEX uq_nte004_edge
ON notes.nte004_page_link (source_page_id, target_page_id, kind);

-- Backlinks ("who points at me") are the hot read.
CREATE INDEX ix_nte004_target_page_id
ON notes.nte004_page_link (target_page_id);

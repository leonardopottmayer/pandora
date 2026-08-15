-- 20260814130001-create-table-nte006-page-tag.up.sql

-- The fact that a page carries a tag, derived from the page's markdown (#tag) and rewritten on every
-- save of that page -- the same mechanic as the wiki edges in nte004.
CREATE TABLE notes.nte006_page_tag (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NOT NULL,
	tag_id uuid NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE notes.nte006_page_tag
ADD CONSTRAINT pk_nte006 PRIMARY KEY (id);

ALTER TABLE notes.nte006_page_tag
ADD CONSTRAINT fk_nte006_page
FOREIGN KEY (page_id) REFERENCES notes.nte001_page (id);

-- A tag that loses its last page is deleted by the save that dropped it (unless it has a color), so
-- the rows never outlive their tag.
ALTER TABLE notes.nte006_page_tag
ADD CONSTRAINT fk_nte006_tag
FOREIGN KEY (tag_id) REFERENCES notes.nte005_tag (id);

-- A tag written five times in one page is one fact.
CREATE UNIQUE INDEX uq_nte006_page_tag
ON notes.nte006_page_tag (page_id, tag_id);

-- "Which pages carry this tag?" is the filter's read.
CREATE INDEX ix_nte006_tag_id
ON notes.nte006_page_tag (tag_id);

-- 20261102130000-create-table-nte010-page-media.up.sql

-- Which library items a page shows, derived from its markdown like the wiki graph (nte004): a save
-- rewrites the page's rows, deleting the page drops them. It is what answers "used in which notes"
-- and finds the items no note uses.
CREATE TABLE notes.nte010_page_media (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NOT NULL,
	media_id uuid NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE notes.nte010_page_media
ADD CONSTRAINT pk_nte010 PRIMARY KEY (id);

ALTER TABLE notes.nte010_page_media
ADD CONSTRAINT fk_nte010_page
FOREIGN KEY (page_id) REFERENCES notes.nte001_page (id);

ALTER TABLE notes.nte010_page_media
ADD CONSTRAINT fk_nte010_media
FOREIGN KEY (media_id) REFERENCES notes.nte002_media (id);

-- An item shown five times on one page is one fact.
CREATE UNIQUE INDEX uq_nte010_page_media
ON notes.nte010_page_media (page_id, media_id);

-- "Which pages use this item?" is the read the library makes.
CREATE INDEX ix_nte010_media_id
ON notes.nte010_page_media (media_id);

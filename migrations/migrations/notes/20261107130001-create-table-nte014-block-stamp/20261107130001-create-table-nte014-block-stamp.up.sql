-- 20261107130001-create-table-nte014-block-stamp.up.sql

-- When each block of a page was written and last changed, and by what (edit, assistant, restore).
-- Derived on every save from a fingerprint of the block, like the links in nte004.
CREATE TABLE notes.nte014_block_stamp (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NOT NULL,
	block_id varchar(64) NOT NULL,
	fingerprint varchar(32) NOT NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NOT NULL,
	updated_source varchar(20) NOT NULL
);

ALTER TABLE notes.nte014_block_stamp
ADD CONSTRAINT pk_nte014 PRIMARY KEY (id);

ALTER TABLE notes.nte014_block_stamp
ADD CONSTRAINT fk_nte014_page
FOREIGN KEY (page_id) REFERENCES notes.nte001_page (id);

-- One stamp per block of a page; the page's set is also the read.
CREATE UNIQUE INDEX uq_nte014_page_block
ON notes.nte014_block_stamp (page_id, block_id);

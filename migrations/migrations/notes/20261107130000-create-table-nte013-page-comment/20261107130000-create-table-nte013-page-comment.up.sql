-- 20261107130000-create-table-nte013-page-comment.up.sql

-- A comment on one block of a page. A thread is its first comment (thread_id null) and the replies
-- pointing at it; only the first carries the quoted text and the resolution.
CREATE TABLE notes.nte013_page_comment (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	page_id uuid NOT NULL,
	user_id uuid NOT NULL,
	block_id varchar(64) NOT NULL,
	thread_id uuid NULL,
	quote varchar(500) NULL,
	body text NOT NULL,
	resolved_at timestamptz NULL,
	created_at timestamptz NOT NULL,
	edited_at timestamptz NULL
);

ALTER TABLE notes.nte013_page_comment
ADD CONSTRAINT pk_nte013 PRIMARY KEY (id);

ALTER TABLE notes.nte013_page_comment
ADD CONSTRAINT fk_nte013_page
FOREIGN KEY (page_id) REFERENCES notes.nte001_page (id);

ALTER TABLE notes.nte013_page_comment
ADD CONSTRAINT fk_nte013_thread
FOREIGN KEY (thread_id) REFERENCES notes.nte013_page_comment (id);

-- A page's comments are read together, when the page opens.
CREATE INDEX ix_nte013_page_id
ON notes.nte013_page_comment (page_id);

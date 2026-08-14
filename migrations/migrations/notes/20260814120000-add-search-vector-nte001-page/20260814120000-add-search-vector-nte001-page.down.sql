-- 20260814120000-add-search-vector-nte001-page.down.sql

DROP INDEX notes.ix_nte001_search_vector;

ALTER TABLE notes.nte001_page
DROP COLUMN search_vector;

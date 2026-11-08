-- 20261107120000-create-table-nte012-folder.down.sql

ALTER TABLE notes.nte001_page DROP CONSTRAINT fk_nte001_folder;

DROP TABLE notes.nte012_folder;

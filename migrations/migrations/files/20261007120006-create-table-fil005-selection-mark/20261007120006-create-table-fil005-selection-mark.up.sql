-- 20261007120006-create-table-fil005-selection-mark.up.sql

-- Which folders of a root are in: a folder takes the mode of its deepest marked ancestor (or its own
-- mark); with no mark at all, it is included. "/" is the root itself.
CREATE TABLE files.fil005_selection_mark (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	root_id uuid NOT NULL,
	path TEXT NOT NULL,
	mode VARCHAR(20) NOT NULL
);

ALTER TABLE files.fil005_selection_mark
ADD CONSTRAINT pk_fil005 PRIMARY KEY (id);

ALTER TABLE files.fil005_selection_mark
ADD CONSTRAINT uq_fil005_root_path UNIQUE (root_id, path);

ALTER TABLE files.fil005_selection_mark
ADD CONSTRAINT fk_fil005_root_id FOREIGN KEY (root_id)
REFERENCES files.fil001_root (id) ON DELETE CASCADE;

ALTER TABLE files.fil005_selection_mark
ADD CONSTRAINT chk_fil005_mode
CHECK (mode IN ('include', 'exclude'));

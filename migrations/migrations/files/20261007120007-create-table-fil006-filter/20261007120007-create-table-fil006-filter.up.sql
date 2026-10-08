-- 20261007120007-create-table-fil006-filter.up.sql

-- A name rule on top of the selection. Scope is the most specific of: scope_path (a folder of root_id),
-- root_id, device_id, or the whole user. An exclude always wins over an include.
CREATE TABLE files.fil006_filter (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	device_id uuid NULL,
	root_id uuid NULL,
	scope_path TEXT NULL,
	name VARCHAR(100) NOT NULL,
	action VARCHAR(20) NOT NULL,
	applies_to VARCHAR(20) NOT NULL,
	matcher VARCHAR(20) NOT NULL,
	pattern VARCHAR(500) NOT NULL,
	case_sensitive BOOLEAN NULL,
	is_enabled BOOLEAN NOT NULL DEFAULT true,
	is_builtin BOOLEAN NOT NULL DEFAULT false,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE files.fil006_filter
ADD CONSTRAINT pk_fil006 PRIMARY KEY (id);

ALTER TABLE files.fil006_filter
ADD CONSTRAINT fk_fil006_root_id FOREIGN KEY (root_id)
REFERENCES files.fil001_root (id) ON DELETE CASCADE;

ALTER TABLE files.fil006_filter
ADD CONSTRAINT chk_fil006_action
CHECK (action IN ('include', 'exclude'));

ALTER TABLE files.fil006_filter
ADD CONSTRAINT chk_fil006_applies_to
CHECK (applies_to IN ('file', 'folder'));

ALTER TABLE files.fil006_filter
ADD CONSTRAINT chk_fil006_matcher
CHECK (matcher IN ('extension', 'glob', 'starts-with', 'ends-with', 'contains', 'regex'));

-- A folder scope only makes sense inside a root.
ALTER TABLE files.fil006_filter
ADD CONSTRAINT chk_fil006_scope_path
CHECK (scope_path IS NULL OR root_id IS NOT NULL);

CREATE INDEX ix_fil006_user_id ON files.fil006_filter (user_id);

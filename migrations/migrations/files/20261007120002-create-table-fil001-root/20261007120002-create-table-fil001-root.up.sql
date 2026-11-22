-- 20261007120002-create-table-fil001-root.up.sql

-- A top-level location on a paired device (a whole disk or a folder) that its agent scans.
-- device_id references identity.idt009_device only logically: no FK leaves the files schema.
CREATE TABLE files.fil001_root (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	device_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	local_path VARCHAR(1024) NOT NULL,
	kind VARCHAR(20) NOT NULL DEFAULT 'folder',
	case_sensitive BOOLEAN NOT NULL,
	include_hidden BOOLEAN NOT NULL DEFAULT false,
	scan_time TIME NULL,
	status VARCHAR(20) NOT NULL DEFAULT 'active',
	last_completed_scan_at TIMESTAMPTZ NULL,
	entry_count INTEGER NOT NULL DEFAULT 0,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE files.fil001_root
ADD CONSTRAINT pk_fil001 PRIMARY KEY (id);

ALTER TABLE files.fil001_root
ADD CONSTRAINT uq_fil001_device_local_path UNIQUE (device_id, local_path);

ALTER TABLE files.fil001_root
ADD CONSTRAINT chk_fil001_kind
CHECK (kind IN ('folder'));

ALTER TABLE files.fil001_root
ADD CONSTRAINT chk_fil001_status
CHECK (status IN ('active', 'removed'));

CREATE INDEX ix_fil001_user_id ON files.fil001_root (user_id);

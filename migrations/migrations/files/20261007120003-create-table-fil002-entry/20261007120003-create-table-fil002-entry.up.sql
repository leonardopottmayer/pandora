-- 20261007120003-create-table-fil002-entry.up.sql

-- One file or folder under a root, by its path relative to the root ("/Movies/a.mkv", "/" is the root).
-- Never deleted by a scan: missing and excluded entries wait in the review inbox (kept_at = the user
-- chose to keep it out of the inbox). metadata is what the bytes say (F2: EXIF, duration, tags, PDF
-- pages); NULL until the agent reads it, '{}' when it found nothing.
CREATE TABLE files.fil002_entry (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	root_id uuid NOT NULL,
	kind VARCHAR(20) NOT NULL,
	relative_path TEXT NOT NULL,
	parent_path TEXT NOT NULL,
	name TEXT NOT NULL,
	extension VARCHAR(50) NULL,
	category VARCHAR(20) NULL,
	size_bytes BIGINT NOT NULL DEFAULT 0,
	modified_at TIMESTAMPTZ NULL,
	fingerprint VARCHAR(64) NULL,
	metadata JSONB NULL,
	search_text TEXT GENERATED ALWAYS AS (
		name
		|| coalesce(' ' || (metadata ->> 'Title'), '')
		|| coalesce(' ' || (metadata ->> 'Artist'), '')
		|| coalesce(' ' || (metadata ->> 'Album'), '')
	) STORED,
	status VARCHAR(20) NOT NULL DEFAULT 'present',
	missing_since TIMESTAMPTZ NULL,
	kept_at TIMESTAMPTZ NULL,
	first_seen_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	last_seen_scan_id uuid NULL
);

ALTER TABLE files.fil002_entry
ADD CONSTRAINT pk_fil002 PRIMARY KEY (id);

ALTER TABLE files.fil002_entry
ADD CONSTRAINT uq_fil002_root_relative_path UNIQUE (root_id, relative_path);

ALTER TABLE files.fil002_entry
ADD CONSTRAINT fk_fil002_root_id FOREIGN KEY (root_id)
REFERENCES files.fil001_root (id) ON DELETE CASCADE;

ALTER TABLE files.fil002_entry
ADD CONSTRAINT chk_fil002_kind
CHECK (kind IN ('file', 'directory'));

ALTER TABLE files.fil002_entry
ADD CONSTRAINT chk_fil002_status
CHECK (status IN ('present', 'missing', 'excluded'));

ALTER TABLE files.fil002_entry
ADD CONSTRAINT chk_fil002_category
CHECK (category IN ('video', 'audio', 'image', 'document', 'ebook', 'archive', 'code', 'other'));

-- Browsing: the children of one folder.
CREATE INDEX ix_fil002_root_parent_path ON files.fil002_entry (root_id, parent_path);

-- Move detection: the same file seen at another path, in any root of the user.
CREATE INDEX ix_fil002_user_fingerprint ON files.fil002_entry (user_id, fingerprint)
WHERE fingerprint IS NOT NULL;

-- The review inbox: what is missing or excluded and not yet decided.
CREATE INDEX ix_fil002_review ON files.fil002_entry (root_id, relative_path)
WHERE status <> 'present' AND kept_at IS NULL;

-- Search by fragments of the name, title, artist or album.
CREATE INDEX ix_fil002_search_text_trgm ON files.fil002_entry USING gin (search_text gin_trgm_ops);

CREATE INDEX ix_fil002_user_id ON files.fil002_entry (user_id);

-- 20261007120004-create-table-fil003-scan.up.sql

-- One walk of one root by its agent. Only a completed scan marks entries missing; an aborted (or
-- timed-out) one is discarded, and a held one waits for the user (safety brake).
CREATE TABLE files.fil003_scan (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	root_id uuid NOT NULL,
	status VARCHAR(20) NOT NULL,
	started_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	last_batch_at TIMESTAMPTZ NULL,
	finished_at TIMESTAMPTZ NULL,
	seen INTEGER NOT NULL DEFAULT 0,
	created INTEGER NOT NULL DEFAULT 0,
	changed INTEGER NOT NULL DEFAULT 0,
	moved INTEGER NOT NULL DEFAULT 0,
	missing INTEGER NOT NULL DEFAULT 0,
	excluded INTEGER NOT NULL DEFAULT 0,
	error VARCHAR(200) NULL
);

ALTER TABLE files.fil003_scan
ADD CONSTRAINT pk_fil003 PRIMARY KEY (id);

ALTER TABLE files.fil003_scan
ADD CONSTRAINT fk_fil003_root_id FOREIGN KEY (root_id)
REFERENCES files.fil001_root (id) ON DELETE CASCADE;

ALTER TABLE files.fil003_scan
ADD CONSTRAINT chk_fil003_status
CHECK (status IN ('running', 'completed', 'aborted', 'held'));

CREATE INDEX ix_fil003_root_started_at ON files.fil003_scan (root_id, started_at DESC);

-- At most one running scan per root.
CREATE UNIQUE INDEX uq_fil003_root_running ON files.fil003_scan (root_id)
WHERE status = 'running';

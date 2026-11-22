-- 20261007120005-create-table-fil004-preferences.up.sql

-- The account switch: off by default; agents get no roots while it is off.
CREATE TABLE files.fil004_preferences (
	user_id uuid NOT NULL,
	is_enabled BOOLEAN NOT NULL DEFAULT false,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE files.fil004_preferences
ADD CONSTRAINT pk_fil004 PRIMARY KEY (user_id);

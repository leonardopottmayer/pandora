-- 20261106120001-create-table-adm001-feature-flag.up.sql

-- Only the flags an administrator changed. A flag's default lives in code (the catalog); a key here
-- that the code no longer declares is ignored.
CREATE TABLE admin.adm001_feature_flag (
	key VARCHAR(150) NOT NULL,
	enabled BOOLEAN NOT NULL,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE admin.adm001_feature_flag
ADD CONSTRAINT pk_adm001 PRIMARY KEY (key);

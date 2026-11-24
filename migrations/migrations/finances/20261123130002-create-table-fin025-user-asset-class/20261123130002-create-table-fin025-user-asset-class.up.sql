-- 20261123130002-create-table-fin025-user-asset-class.up.sql
-- The user's own asset classes, for what the system list (fin020) does not have. They can be renamed
-- and archived; the behavior is fixed at creation, since positions depend on it.

CREATE TABLE finances.fin025_user_asset_class (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(60) NOT NULL,
	behavior VARCHAR(20) NOT NULL,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin025_user_asset_class
ADD CONSTRAINT pk_fin025 PRIMARY KEY (id);

ALTER TABLE finances.fin025_user_asset_class
ADD CONSTRAINT uq_fin025_user_name UNIQUE (user_id, name);

ALTER TABLE finances.fin025_user_asset_class
ADD CONSTRAINT ck_fin025_behavior CHECK (behavior IN ('unit', 'application'));

CREATE INDEX ix_fin025_user_id
ON finances.fin025_user_asset_class (user_id);
